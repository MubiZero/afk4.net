# Сотрудники платформы и 2FA — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать платформе управление собственными сотрудниками (список, приглашение по коду, роль, отключение) и обязательный второй фактор при входе, заменив заглушку `UnavailableScreen` на маршруте `/admin/settings` рабочим экраном.

**Architecture:** Роли остаются захардкоженными двумя (`platform_admin`, `platform_support`) — управление сводится к списку, приглашению и переключателю активности. Приглашение повторяет модель владельцев клубов: запись с хэшем кода → экран активации → создание учётки. Вход становится двухшаговым через отдельную сущность-челлендж: пароль выдаёт короткоживущий промежуточный токен, который открывает только проверку кода и настройку 2FA, а рабочую пару access/refresh выдаёт уже `IPlatformAdminTokenService.IssueAsync` после подтверждения.

**Tech Stack:** ASP.NET Core minimal APIs + EF Core 10/Npgsql (`AFK4.Platform.Api`), xUnit + `PlatformApiFactory` (`tests/AFK4.Platform.Api.Tests`), React 19 + TypeScript + Vite (`AFK4.PlatformControl.Web`), `bun test` + happy-dom.

## Global Constraints

- Спека: `docs/superpowers/specs/2026-08-04-platform-access-and-support-mode-design.md`. Этот план покрывает слайсы 1–2 из четырёх; режим поддержки (слайсы 3–4) — отдельный план.
- **Новые NuGet-пакеты запрещены.** В `AFK4.Platform.Api.csproj` пять зависимостей, TOTP реализуется на `System.Security.Cryptography`.
- `BUN=/home/fedya/.bun/bin/bun` — bun не на PATH, вызывать полным путём.
- Строки UI — только через `@afk4/i18n`. Источник истины — `locales/ru.json`, `locales/en.json`, `locales/tg.json` в корне репозитория; после правки запускать `cd packages/i18n && "$BUN" run gen` (он перегенерирует `src/messages.*.ts`). Хардкод-строк в компонентах быть не должно.
- Таджикский — настоящий таджикский: guard-тест падает на `tg === ru` вне whitelist заимствований.
- Зелёный `bun test` не равен зелёной сборке: `bun run build` = `tsc -b && vite build` и тайпчекает в том числе тест-файлы. Каждая фронтовая задача заканчивается сборкой.
- Секреты не попадают в логи, аудит и ответы API: TOTP-секрет отдаётся клиенту ровно один раз при настройке, коды восстановления — один раз при генерации, дальше только хэши.
- Каждая мутация пишет аудит через существующий `IAuditRecordWriter` с исходом `Succeeded`/`Denied`, по образцу соседних платформенных эндпоинтов.
- Имена по назначению: сущность в UI называется «сотрудник платформы», а не «admin user».
- Сообщения коммитов — на русском с conventional-префиксом, как в истории репозитория (`feat(platform): ...`).
- Прогон бэкенд-тестов: `dotnet test tests/AFK4.Platform.Api.Tests --filter <FullyQualifiedName~Имя>`.

---

## File Structure

**Бэкенд (`src/AFK4.Platform.Api`)**

| Файл | Ответственность |
|---|---|
| `Data/PlatformAdminInvitationEntity.cs` | новая сущность приглашения сотрудника |
| `Data/PlatformAdminSignInChallengeEntity.cs` | новая сущность промежуточного токена входа |
| `Data/PlatformAdminUserEntity.cs` | + поля 2FA и время последнего входа |
| `Data/PlatformDbContext.cs` | регистрация двух новых наборов и индексов |
| `Platform/Identity/PlatformAdminDirectoryService.cs` | список, приглашение, роль, активность + инварианты |
| `Platform/Identity/PlatformAdminTwoFactorService.cs` | настройка, проверка, коды восстановления, блокировка |
| `Platform/Identity/TotpCodeGenerator.cs` | чистый RFC 6238: секрет + время → код, и base32 для `otpauth://` |
| `Endpoints/PlatformAdminDirectoryEndpoints.cs` | `/api/platform/admins*` и активация приглашения |
| `Endpoints/PlatformAdminTwoFactorEndpoints.cs` | `/api/platform/auth/2fa/*` |

Каталог сотрудников и второй фактор разведены по разным сервисам и файлам эндпоинтов намеренно: у них разные потребители (первый — только под правом `platform.admins.manage`, второй — частично анонимный, по промежуточному токену).

**Контракты (`src/AFK4.Shared.Contracts/Platform/Auth`)**: `PlatformAdminPermissionNames.cs` (+1 право), новые DTO приглашений, каталога и 2FA.

**Фронт (`src/AFK4.PlatformControl.Web/src`)**

| Файл | Ответственность |
|---|---|
| `api/platformClients/admins.ts` | сабклиент каталога сотрудников |
| `api/platformClients/twoFactor.ts` | сабклиент 2FA |
| `platform/settings/SettingsScreen.tsx` | экран раздела «Настройки» |
| `platform/settings/useAdmins.ts` | загрузка каталога, по образцу `usePlans.ts` |
| `platform/settings/adminsModel.ts` | чистые правила отображения и блокировки действий |
| `platform/settings/AdminInviteDialog.tsx` | диалог приглашения |
| `components/TwoFactorChallenge.tsx` | шаг ввода кода при входе |
| `components/TwoFactorSetup.tsx` | QR, подтверждение, коды восстановления |

---

## Task 1: Право `platform.admins.manage`

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminPermissionCatalogTests.cs`

**Interfaces:**
- Produces: `PlatformAdminPermissionNames.ManagePlatformAdmins = "platform.admins.manage"`, входит только в роль `platform_admin`.

- [ ] **Step 1: Написать падающий тест**

```csharp
[Fact]
public void ManagePlatformAdmins_BelongsToFullAdminRoleOnly()
{
    var adminPermissions = PlatformAdminPermissionCatalog.GetPermissions([PlatformAdminRoleNames.PlatformAdmin]);
    var supportPermissions = PlatformAdminPermissionCatalog.GetPermissions([PlatformAdminRoleNames.PlatformSupport]);

    Assert.Contains(PlatformAdminPermissionNames.ManagePlatformAdmins, adminPermissions);
    Assert.DoesNotContain(PlatformAdminPermissionNames.ManagePlatformAdmins, supportPermissions);
}
```

- [ ] **Step 2: Прогнать и убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminPermissionCatalogTests`
Expected: ошибка компиляции — `ManagePlatformAdmins` не существует.

- [ ] **Step 3: Добавить константу и включить в роль**

В `PlatformAdminPermissionNames.cs`:

```csharp
public const string ManagePlatformAdmins = "platform.admins.manage";
```

В `PlatformAdminPermissionCatalog.cs`, в набор `PlatformAdminRoleNames.PlatformAdmin`, добавить строку `PlatformAdminPermissionNames.ManagePlatformAdmins,`. Набор `PlatformSupport` не трогать.

- [ ] **Step 4: Прогнать и убедиться, что проходит**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminPermissionCatalogTests`
Expected: PASS.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminPermissionCatalogTests.cs
git commit -m "feat(platform): право platform.admins.manage для полной роли"
```

---

## Task 2: Сущности приглашения и челленджа, поля пользователя, миграция

**Files:**
- Create: `src/AFK4.Platform.Api/Data/PlatformAdminInvitationEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/PlatformAdminSignInChallengeEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformAdminUserEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryPersistenceTests.cs`

**Interfaces:**
- Produces: `PlatformAdminInvitationEntity` (`InvitationId`, `CodeHash` byte[], `Role`, `Status`, `ExpiresAtUtc`, `CreatedByPlatformAdminUserId`, `CreatedAtUtc`, `AcceptedAtUtc?`, `AcceptedPlatformAdminUserId?`, `RevokedAtUtc?`), `PlatformAdminSignInChallengeEntity` (`ChallengeId`, `PlatformAdminUserId`, `TokenHash` byte[], `ExpiresAtUtc`, `ConsumedAtUtc?`, `FailedAttempts` int), и поля `PlatformAdminUserEntity`: `TotpSecretEncrypted string?`, `TotpEnabledAtUtc DateTimeOffset?`, `RecoveryCodeHashesJson string` (по умолчанию `"[]"`), `FailedTwoFactorAttempts int`, `TwoFactorLockedUntilUtc DateTimeOffset?`, `LastSignInAtUtc DateTimeOffset?`.
- Consumes: ничего.

- [ ] **Step 1: Написать падающий тест persistence**

```csharp
public sealed class PlatformAdminDirectoryPersistenceTests
{
    [Fact]
    public async Task Invitation_RoundTripsAndCodeHashIsUnique()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var hash = new byte[] { 1, 2, 3, 4 };

        db.PlatformAdminInvitations.Add(new PlatformAdminInvitationEntity
        {
            InvitationId = Guid.NewGuid(),
            CodeHash = hash,
            Role = PlatformAdminRoleNames.PlatformSupport,
            Status = "pending",
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddDays(3),
            CreatedByPlatformAdminUserId = Guid.NewGuid(),
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();

        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.Equal("pending", stored.Status);
        Assert.Equal(hash, stored.CodeHash);
    }

    [Fact]
    public async Task AdminUser_HasTwoFactorColumnsWithSafeDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var user = await db.PlatformAdminUsers.FirstAsync();

        Assert.Null(user.TotpSecretEncrypted);
        Assert.Null(user.TotpEnabledAtUtc);
        Assert.Equal("[]", user.RecoveryCodeHashesJson);
        Assert.Equal(0, user.FailedTwoFactorAttempts);
    }
}
```

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryPersistenceTests`
Expected: ошибка компиляции — типов и свойств нет.

- [ ] **Step 3: Завести сущности и поля**

`Data/PlatformAdminInvitationEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminInvitationEntity
{
    public Guid InvitationId { get; set; }

    public byte[] CodeHash { get; set; } = [];

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedPlatformAdminUserId { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
```

`Data/PlatformAdminSignInChallengeEntity.cs`:

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminSignInChallengeEntity
{
    public Guid ChallengeId { get; set; }

    public Guid PlatformAdminUserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }

    public int FailedAttempts { get; set; }
}
```

В `PlatformAdminUserEntity` дописать шесть свойств из блока Interfaces. В `PlatformDbContext` добавить `DbSet<PlatformAdminInvitationEntity> PlatformAdminInvitations` и `DbSet<PlatformAdminSignInChallengeEntity> PlatformAdminSignInChallenges`, а в `OnModelCreating` — уникальные индексы по `CodeHash` и `TokenHash`, по образцу соседних токен-сущностей.

- [ ] **Step 4: Сгенерировать миграцию**

```bash
dotnet ef migrations add AddPlatformAdminDirectoryAndTwoFactor --project src/AFK4.Platform.Api
dotnet ef migrations script --project src/AFK4.Platform.Api --idempotent | head -80
```

Убедиться глазами, что скрипт трогает только `platform_admin_users` (шесть новых колонок) и две новые таблицы. Если в diff всплыли чужие таблицы — миграцию удалить и разбираться, а не коммитить.

- [ ] **Step 5: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryPersistenceTests`
Expected: PASS.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.Platform.Api/Data tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryPersistenceTests.cs
git commit -m "feat(platform): схема каталога сотрудников и второго фактора"
```

---

## Task 3: Служба каталога сотрудников и её инварианты

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminDirectoryContracts.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryServiceTests.cs`

**Interfaces:**
- Consumes: `PlatformAdminInvitationEntity`, `PlatformAdminUserEntity` (Task 2), `PlatformAdminPermissionNames.ManagePlatformAdmins` (Task 1).
- Produces:
  - `record PlatformAdminListItem(Guid PlatformAdminUserId, string UserName, string DisplayName, string Role, bool IsActive, bool TwoFactorEnabled, DateTimeOffset? LastSignInAtUtc, DateTimeOffset CreatedAtUtc)`
  - `record PlatformAdminInvitationDto(Guid InvitationId, string Role, string Status, DateTimeOffset ExpiresAtUtc, DateTimeOffset CreatedAtUtc)`
  - `record CreatePlatformAdminInvitationRequest(string Role, int LifetimeHours)`
  - `record CreatePlatformAdminInvitationResponse(PlatformAdminInvitationDto Invitation, string Code)` — код отдаётся один раз, при создании
  - `record UpdatePlatformAdminRequest(string? Role, bool? IsActive)`
  - `enum PlatformAdminDirectoryError { None, LastFullAdmin, SelfDemotion, NotFound, UnknownRole }`
  - методы: `Task<IReadOnlyList<PlatformAdminListItem>> ListAsync(CancellationToken)`, `Task<IReadOnlyList<PlatformAdminInvitationDto>> ListInvitationsAsync(CancellationToken)`, `Task<(CreatePlatformAdminInvitationResponse? Response, PlatformAdminDirectoryError Error)> InviteAsync(Guid actorId, CreatePlatformAdminInvitationRequest request, CancellationToken)`, `Task<(PlatformAdminListItem? Item, PlatformAdminDirectoryError Error)> UpdateAsync(Guid actorId, Guid targetId, UpdatePlatformAdminRequest request, CancellationToken)`, `Task<PlatformAdminDirectoryError> RevokeInvitationAsync(Guid invitationId, CancellationToken)`.

- [ ] **Step 1: Написать падающие тесты инвариантов**

```csharp
public sealed class PlatformAdminDirectoryServiceTests
{
    [Fact]
    public async Task DisablingLastFullAdmin_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (item, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(null, false), CancellationToken.None);

        Assert.Null(item);
        Assert.Equal(PlatformAdminDirectoryError.LastFullAdmin, error);
    }

    [Fact]
    public async Task DemotingSelf_IsRejectedEvenWhenAnotherAdminExists()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        db.PlatformAdminUsers.Add(new PlatformAdminUserEntity
        {
            PlatformAdminUserId = Guid.NewGuid(),
            UserName = "second",
            NormalizedUserName = "SECOND",
            DisplayName = "Второй админ",
            PasswordHash = "x",
            RolesJson = OpaquePlatformAdminTokenService.SerializeRoles([PlatformAdminRoleNames.PlatformAdmin]),
            IsActive = true,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.UpdateAsync(admin.PlatformAdminId, admin.PlatformAdminId,
            new UpdatePlatformAdminRequest(PlatformAdminRoleNames.PlatformSupport, null), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.SelfDemotion, error);
    }

    [Fact]
    public async Task Invitation_ReturnsCodeOnceAndStoresOnlyHash()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (response, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.None, error);
        Assert.False(string.IsNullOrWhiteSpace(response!.Code));
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await db.PlatformAdminInvitations.SingleAsync();
        Assert.DoesNotContain(response.Code, System.Text.Encoding.UTF8.GetString(stored.CodeHash));
    }

    [Fact]
    public async Task UnknownRole_IsRejected()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<PlatformAdminDirectoryService>();

        var (_, error) = await service.InviteAsync(admin.PlatformAdminId,
            new CreatePlatformAdminInvitationRequest("platform_god", 24), CancellationToken.None);

        Assert.Equal(PlatformAdminDirectoryError.UnknownRole, error);
    }
}
```

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryServiceTests`
Expected: ошибка компиляции — службы и контрактов нет.

- [ ] **Step 3: Реализовать службу**

Правила, которые обязана соблюдать `UpdateAsync`, в этом порядке:

1. цель не найдена → `NotFound`;
2. `actorId == targetId` и запрошено понижение роли или `IsActive == false` → `SelfDemotion`;
3. роль неизвестна `PlatformAdminPermissionCatalog.IsKnownRole` → `UnknownRole`;
4. цель — активный `platform_admin`, и после изменения число активных `platform_admin` станет нулём → `LastFullAdmin`;
5. иначе применить, обновить `UpdatedAtUtc`.

Код приглашения — 32 случайных символа из `RandomNumberGenerator`, алфавит без похожих знаков (`0`, `O`, `1`, `l`), в базе только `SHA256` от него. Регистрация в `Program.cs`: `builder.Services.AddScoped<PlatformAdminDirectoryService>();`

- [ ] **Step 4: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryServiceTests`
Expected: PASS, все четыре теста.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminDirectoryContracts.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryServiceTests.cs
git commit -m "feat(platform): служба каталога сотрудников с защитой от запирания панели"
```

---

## Task 4: Эндпоинты каталога и приглашений

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformAdminDirectoryEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryEndpointTests.cs`

**Interfaces:**
- Consumes: `PlatformAdminDirectoryService` и его DTO (Task 3), `PlatformAdminAuthorizationService`.
- Produces: `GET /api/platform/admins`, `POST /api/platform/admins/invitations`, `POST /api/platform/admins/invitations/{invitationId:guid}/revoke`, `PATCH /api/platform/admins/{platformAdminUserId:guid}` — все под `PlatformAdminPermissionNames.ManagePlatformAdmins`.

- [ ] **Step 1: Написать падающие тесты**

```csharp
public sealed class PlatformAdminDirectoryEndpointTests
{
    [Fact]
    public async Task SupportRole_CannotSeeDirectory()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformSupport]);

        var response = await client.GetAsync("/api/platform/admins");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task FullAdmin_ListsDirectoryWithSelf()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var items = await client.GetFromJsonAsync<PlatformAdminListItem[]>("/api/platform/admins");

        Assert.NotNull(items);
        Assert.Contains(items!, item => item.PlatformAdminUserId == admin.PlatformAdminId && item.IsActive);
    }

    [Fact]
    public async Task DisablingLastFullAdmin_Returns409()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var admin = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var response = await client.PatchAsJsonAsync($"/api/platform/admins/{admin.PlatformAdminId:D}",
            new UpdatePlatformAdminRequest(null, false));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Invitation_IsListedAsPendingAndRevocable()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
            new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
        var body = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
        var revoked = await client.PostAsync($"/api/platform/admins/invitations/{body!.Invitation.InvitationId:D}/revoke", null);

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("pending", body.Invitation.Status);
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
    }
}
```

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryEndpointTests`
Expected: 404/ошибка компиляции — маршрутов нет.

- [ ] **Step 3: Реализовать эндпоинты**

Форма — как в соседних платформенных эндпоинтах: `authorizationService.RequireAuthenticated()` → проверка права → вызов службы → `IAuditRecordWriter.WriteAsync` с `Action` из `AuditActionNames` (добавить `PlatformAdminInvited`, `PlatformAdminUpdated`, `PlatformAdminInvitationRevoked`) → результат. Отображение ошибок службы: `NotFound` → 404, `UnknownRole` → 400, `LastFullAdmin` и `SelfDemotion` → 409 с текстом причины в теле. Регистрация в `Program.cs` рядом с `MapPlatformOrganizationEndpoints()`.

- [ ] **Step 4: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminDirectoryEndpointTests`
Expected: PASS, все четыре теста.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Endpoints/PlatformAdminDirectoryEndpoints.cs src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.Api/Audit tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminDirectoryEndpointTests.cs
git commit -m "feat(platform): эндпоинты каталога сотрудников платформы"
```

---

## Task 5: Активация приглашения

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformAdminDirectoryEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminInvitationActivationTests.cs`

**Interfaces:**
- Consumes: `PlatformAdminInvitationEntity`, `PlatformAdminDirectoryService`.
- Produces: анонимный `POST /api/account-activation/platform-admin` с телом `record AcceptPlatformAdminInvitationRequest(string Code, string UserName, string DisplayName, string Password)`; ответ `204 No Content` (вход — обычным путём, чтобы сразу сработала 2FA из Task 8–10).

- [ ] **Step 1: Написать падающие тесты**

```csharp
[Fact]
public async Task ValidCode_CreatesActiveAdminWithInvitedRole()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
    var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
        new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
    var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();

    using var anonymous = factory.CreateClient();
    var accepted = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
        new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

    Assert.Equal(HttpStatusCode.NoContent, accepted.StatusCode);
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var user = await db.PlatformAdminUsers.SingleAsync(x => x.NormalizedUserName == "SUPPORT1");
    Assert.True(user.IsActive);
    Assert.Contains(PlatformAdminRoleNames.PlatformSupport, user.RolesJson);
}

[Fact]
public async Task CodeCannotBeUsedTwice()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
    var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
        new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
    var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
    using var anonymous = factory.CreateClient();
    await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
        new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

    var second = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
        new AcceptPlatformAdminInvitationRequest(invitation.Code, "support2", "Вторая поддержка", "S3cret!passphrase"));

    Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
}

[Fact]
public async Task ExpiredCode_IsRejected()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client, roles: [PlatformAdminRoleNames.PlatformAdmin]);
    var created = await client.PostAsJsonAsync("/api/platform/admins/invitations",
        new CreatePlatformAdminInvitationRequest(PlatformAdminRoleNames.PlatformSupport, 72));
    var invitation = await created.Content.ReadFromJsonAsync<CreatePlatformAdminInvitationResponse>();
    await using (var scope = factory.Services.CreateAsyncScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        (await db.PlatformAdminInvitations.SingleAsync()).ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(-1);
        await db.SaveChangesAsync();
    }

    using var anonymous = factory.CreateClient();
    var response = await anonymous.PostAsJsonAsync("/api/account-activation/platform-admin",
        new AcceptPlatformAdminInvitationRequest(invitation!.Code, "support1", "Первая поддержка", "S3cret!passphrase"));

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
}
```

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminInvitationActivationTests`
Expected: 404 — маршрута нет.

- [ ] **Step 3: Реализовать активацию**

Поиск приглашения по `SHA256` кода среди записей со `Status == "pending"`, `RevokedAtUtc == null` и `ExpiresAtUtc > now`. Занятый логин → 409. Успех: создать пользователя (`PasswordHasher<PlatformAdminUserEntity>`, как в `PasswordHashingPlatformAdminCredentialService`), пометить приглашение `accepted`, записать `AcceptedPlatformAdminUserId`, написать аудит `PlatformAdminInvitationAccepted`. Любая неудача поиска — одинаковый 400 без деталей: подсказывать, «код есть, но истёк» против «кода нет», незачем.

- [ ] **Step 4: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminInvitationActivationTests`
Expected: PASS, три теста.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Endpoints/PlatformAdminDirectoryEndpoints.cs tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminInvitationActivationTests.cs
git commit -m "feat(platform): активация приглашения сотрудника по коду"
```

---

## Task 6: Фронт — сабклиент каталога

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/admins.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts`
- Test: `src/AFK4.PlatformControl.Web/src/api/platformClients/admins.test.ts`

**Interfaces:**
- Consumes: эндпоинты Task 4, `PlatformTransport`.
- Produces: `client.admins` с методами `listAdmins(): Promise<PlatformAdminListItem[]>`, `listInvitations(): Promise<PlatformAdminInvitation[]>`, `invite(role: string, lifetimeHours: number): Promise<CreateInvitationResponse>`, `revokeInvitation(invitationId: string): Promise<void>`, `updateAdmin(platformAdminUserId: string, patch: { role?: string; isActive?: boolean }): Promise<PlatformAdminListItem>`. TS-типы в `api/types.ts`: `PlatformAdminListItem`, `PlatformAdminInvitation`, `CreateInvitationResponse`.

- [ ] **Step 1: Написать падающий тест**

```ts
import { describe, expect, it } from 'bun:test';
import { AdminsApi } from './admins';
import { PlatformTransport } from '../platformTransport';

function transportWith(recorder: { method?: string; path?: string; body?: unknown }): PlatformTransport {
  return {
    send: async (method: string, path: string, body?: unknown) => {
      recorder.method = method;
      recorder.path = path;
      recorder.body = body;
      return [] as unknown;
    }
  } as unknown as PlatformTransport;
}

describe('AdminsApi', () => {
  it('патчит сотрудника по идентификатору', async () => {
    const recorder: { method?: string; path?: string; body?: unknown } = {};
    const api = new AdminsApi(transportWith(recorder));

    await api.updateAdmin('11111111-1111-1111-1111-111111111111', { isActive: false });

    expect(recorder.method).toBe('PATCH');
    expect(recorder.path).toBe('/api/platform/admins/11111111-1111-1111-1111-111111111111');
    expect(recorder.body).toEqual({ role: undefined, isActive: false });
  });

  it('создаёт приглашение с ролью и сроком', async () => {
    const recorder: { method?: string; path?: string; body?: unknown } = {};
    const api = new AdminsApi(transportWith(recorder));

    await api.invite('platform_support', 72);

    expect(recorder.method).toBe('POST');
    expect(recorder.path).toBe('/api/platform/admins/invitations');
    expect(recorder.body).toEqual({ role: 'platform_support', lifetimeHours: 72 });
  });
});
```

- [ ] **Step 2: Прогнать RED**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/api/platformClients/admins.test.ts`
Expected: FAIL — модуля нет.

- [ ] **Step 3: Реализовать сабклиент**

Один в один по образцу `supportNotes.ts`: класс над `PlatformTransport`, без своей логики ошибок. Зарегистрировать поле `public readonly admins: AdminsApi;` в `PlatformApiClient` и проинициализировать в конструкторе.

- [ ] **Step 4: Прогнать GREEN**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/api/platformClients/admins.test.ts`
Expected: PASS.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src/api
git commit -m "feat(platform-control): сабклиент каталога сотрудников"
```

---

## Task 7: Фронт — экран «Настройки → Сотрудники платформы»

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/SettingsScreen.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/useAdmins.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/adminsModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/settings/AdminInviteDialog.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/auth/platformAccess.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/App.tsx:106-112`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `src/AFK4.PlatformControl.Web/src/platform/settings/adminsModel.test.ts`, `src/AFK4.PlatformControl.Web/src/platform/settings/SettingsScreen.test.tsx`

**Interfaces:**
- Consumes: `client.admins` (Task 6), `can(session, 'admins.manage')`.
- Produces: `SettingsScreen({ client, session }: { client: AdminsApi; session: PlatformAdminSession })`; чистые функции `adminsModel.ts`: `canDisable(item: PlatformAdminListItem, selfId: string, items: PlatformAdminListItem[]): boolean`, `canChangeRole(item, selfId, items): boolean`, `roleLabelKey(role: string): string`.

- [ ] **Step 1: Написать падающие тесты правил отображения**

```ts
import { describe, expect, it } from 'bun:test';
import { canDisable, canChangeRole } from './adminsModel';

const admin = (id: string, role: string, isActive = true) => ({
  platformAdminUserId: id, userName: id, displayName: id, role, isActive,
  twoFactorEnabled: true, lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
});

describe('adminsModel', () => {
  it('не даёт отключить самого себя', () => {
    const items = [admin('me', 'platform_admin'), admin('other', 'platform_admin')];
    expect(canDisable(items[0], 'me', items)).toBe(false);
  });

  it('не даёт отключить последнего активного полного админа', () => {
    const items = [admin('me', 'platform_admin'), admin('support', 'platform_support')];
    expect(canDisable(items[0], 'other', items)).toBe(false);
  });

  it('разрешает отключить поддержку', () => {
    const items = [admin('me', 'platform_admin'), admin('support', 'platform_support')];
    expect(canDisable(items[1], 'me', items)).toBe(true);
  });

  it('не даёт понизить самого себя', () => {
    const items = [admin('me', 'platform_admin'), admin('other', 'platform_admin')];
    expect(canChangeRole(items[0], 'me', items)).toBe(false);
  });
});
```

И тест экрана:

```tsx
import { describe, expect, it } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { SettingsScreen } from './SettingsScreen';

describe('SettingsScreen', () => {
  it('показывает сотрудников платформы списком', async () => {
    const client = {
      listAdmins: async () => [{
        platformAdminUserId: 'me', userName: 'root', displayName: 'Главный',
        role: 'platform_admin', isActive: true, twoFactorEnabled: true,
        lastSignInAtUtc: null, createdAtUtc: '2026-08-01T00:00:00Z'
      }],
      listInvitations: async () => []
    };

    render(<SettingsScreen client={client as never} session={{ platformAdminId: 'me' } as never} />);

    expect(await screen.findByText('Главный')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Прогнать RED**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/platform/settings`
Expected: FAIL — модулей нет.

- [ ] **Step 3: Реализовать экран и правила**

`useAdmins.ts` — копия структуры `usePlans.ts` (состояния `loading | error | ready` + `retry`), грузит каталог и приглашения. `SettingsScreen.tsx` — `Card` + `Table` из `components/ui`, колонки: сотрудник, роль, 2FA, последний вход, статус; приглашения идут строками того же списка со статусом «ожидает» и действием «отозвать». Кнопки блокируются по `adminsModel`, у заблокированной — `title` с причиной. Диалог приглашения на `components/ui/dialog` показывает код один раз с кнопкой копирования и явным предупреждением, что второй раз он не покажется.

Ключи i18n добавить в три файла `locales/*.json` под префиксом `platform.settings.*`, затем `cd packages/i18n && "$BUN" run gen`.

В `auth/platformAccess.ts` заменить пустую запись `'settings.manage': []` на `'admins.manage': ['platform.admins.manage']` (и обновить оба использования — `nav.ts`, `App.tsx:124`). В `App.tsx` добавить ветку `: route.kind === 'settings' ? <SettingsScreen client={client.admins} session={session} />` перед `: <UnavailableScreen />`.

- [ ] **Step 4: Прогнать GREEN и сборку**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build`
Expected: тесты PASS, сборка без ошибок типов.

- [ ] **Step 5: Проверить i18n-гарды**

Run: `cd packages/i18n && "$BUN" test`
Expected: PASS — паритет ключей и таджикский не равен русскому.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src locales packages/i18n/src
git commit -m "feat(platform-control): экран сотрудников платформы вместо заглушки настроек"
```

---

## Task 8: Генератор TOTP

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Identity/TotpCodeGenerator.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/TotpCodeGeneratorTests.cs`

**Interfaces:**
- Produces: `static class TotpCodeGenerator` с `string Generate(byte[] secret, long unixTimeSeconds, int step = 30, int digits = 6)`, `bool Verify(byte[] secret, string code, long unixTimeSeconds, int allowedDriftSteps = 1)`, `string ToBase32(byte[] secret)`.

- [ ] **Step 1: Написать падающий тест на векторах RFC 6238**

Секрет — ASCII `"12345678901234567890"`; в RFC приведены восьмизначные коды, шестизначный код — их последние шесть цифр.

```csharp
public sealed class TotpCodeGeneratorTests
{
    private static readonly byte[] Secret = System.Text.Encoding.ASCII.GetBytes("12345678901234567890");

    [Theory]
    [InlineData(59L, "287082")]
    [InlineData(1111111109L, "081804")]
    [InlineData(1111111111L, "050471")]
    [InlineData(1234567890L, "005924")]
    [InlineData(2000000000L, "279037")]
    public void Generate_MatchesRfc6238Vectors(long unixTime, string expected)
    {
        Assert.Equal(expected, TotpCodeGenerator.Generate(Secret, unixTime));
    }

    [Fact]
    public void Verify_AcceptsPreviousAndNextStep_ButNotTwoStepsAway()
    {
        var code = TotpCodeGenerator.Generate(Secret, 1234567890L);

        Assert.True(TotpCodeGenerator.Verify(Secret, code, 1234567890L + 30));
        Assert.True(TotpCodeGenerator.Verify(Secret, code, 1234567890L - 30));
        Assert.False(TotpCodeGenerator.Verify(Secret, code, 1234567890L + 90));
    }

    [Fact]
    public void ToBase32_ProducesRfc4648AlphabetWithoutPadding()
    {
        var encoded = TotpCodeGenerator.ToBase32(Secret);

        Assert.Equal("GEZDGNBVGY3TQOJQGEZDGNBVGY3TQOJQ", encoded);
    }
}
```

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~TotpCodeGeneratorTests`
Expected: ошибка компиляции — класса нет.

- [ ] **Step 3: Реализовать генератор**

`Generate`: счётчик `unixTimeSeconds / step` в big-endian 8 байт, `HMACSHA1` на секрете, динамическое усечение (младшие 4 бита последнего байта — смещение), берём 4 байта, гасим старший бит, остаток от `10^digits`, дополняем нулями слева. `Verify` сравнивает коды постоянным по времени `CryptographicOperations.FixedTimeEquals` по всем шагам в пределах дрейфа. `ToBase32` — алфавит RFC 4648 `A–Z2–7`, без набивки.

- [ ] **Step 4: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~TotpCodeGeneratorTests`
Expected: PASS, семь проверок.

- [ ] **Step 5: Коммит**

```bash
git add src/AFK4.Platform.Api/Platform/Identity/TotpCodeGenerator.cs tests/AFK4.Platform.Api.Tests/Platform/TotpCodeGeneratorTests.cs
git commit -m "feat(platform): генератор TOTP по RFC 6238 без внешних зависимостей"
```

---

## Task 9: Второй фактор целиком — служба, эндпоинты, двухшаговый вход

Служба и эндпоинты идут одной задачей намеренно. Переключение `/api/platform/auth/sign-in` на челлендж ломает вход для **всех** платформенных тестов, а чинится он только вместе с маршрутом `2fa/verify`. Разрезать это на два коммита — значит оставить между ними красный набор.

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminTwoFactorService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformAdminTwoFactorEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PasswordHashingPlatformAdminCredentialService.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminSignInResponse.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminTestHelper.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminTwoFactorTests.cs`, `tests/AFK4.Platform.Api.Tests/Platform/TwoFactorTestHelper.cs`

**Interfaces:**
- Consumes: `TotpCodeGenerator` (Task 8), `ISecretProtector`, `PlatformAdminSignInChallengeEntity` (Task 2), `IPlatformAdminTokenService.IssueAsync`, `PlatformAdminPermissionNames.ManagePlatformAdmins` (Task 1).
- Produces маршруты: `POST /api/platform/auth/2fa/setup` (по challenge-токену, отдаёт секрет и `otpauth://`), `POST /api/platform/auth/2fa/setup/confirm` (код → сессия + коды восстановления), `POST /api/platform/auth/2fa/verify` (код → сессия), `POST /api/platform/admins/{platformAdminUserId:guid}/2fa/reset` (под `ManagePlatformAdmins`).
- Produces типы:
  - `record PlatformAdminSignInChallengeResponse(string ChallengeToken, DateTimeOffset ExpiresAtUtc, bool TwoFactorConfigured)` — ответ первого шага;
  - `PlatformAdminTwoFactorService` с `Task<PlatformAdminSignInChallengeResponse> StartChallengeAsync(PlatformAdminUserEntity user, CancellationToken)`, `Task<(string? Secret, string? OtpAuthUri, TwoFactorError Error)> BeginSetupAsync(string challengeToken, CancellationToken)` (у настроенного пользователя возвращает `(null, null, AlreadyConfigured)`), `Task<(PlatformAdminSignInResponse? Session, IReadOnlyList<string> RecoveryCodes, TwoFactorError Error)> CompleteSetupAsync(string challengeToken, string code, CancellationToken)`, `Task<(PlatformAdminSignInResponse? Session, TwoFactorError Error)> VerifyAsync(string challengeToken, string code, CancellationToken)`, `Task<TwoFactorError> ResetAsync(Guid targetPlatformAdminUserId, CancellationToken)`;
  - `enum TwoFactorError { None, InvalidChallenge, InvalidCode, LockedOut, AlreadyConfigured }`.
- Изменение поведения: `SignInAsync` больше **не** возвращает рабочую сессию. Он возвращает `PlatformAdminSignInChallengeResponse`. Это ломающее изменение контракта `/api/platform/auth/sign-in`, фронт чинится в Task 10.

- [ ] **Step 1: Написать падающие тесты**

```csharp
public sealed class PlatformAdminTwoFactorTests
{
    [Fact]
    public async Task PasswordAlone_DoesNotIssueWorkingSession()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory);
        var response = await client.PostAsJsonAsync("/api/platform/auth/sign-in",
            new PlatformAdminSignInRequest(PlatformAdminTestHelper.DefaultUserName, PlatformAdminTestHelper.DefaultPassword));
        var challenge = await response.Content.ReadFromJsonAsync<PlatformAdminSignInChallengeResponse>();

        Assert.NotNull(challenge);
        Assert.False(string.IsNullOrWhiteSpace(challenge!.ChallengeToken));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", challenge.ChallengeToken);
        var organizations = await client.GetAsync("/api/platform/organizations");
        Assert.Equal(HttpStatusCode.Unauthorized, organizations.StatusCode);
    }

    [Fact]
    public async Task SetupThenCorrectCode_IssuesSessionAndReturnsRecoveryCodesOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);
        var setup = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);
        var code = TotpCodeGenerator.Generate(TwoFactorTestHelper.DecodeBase32(setup.Secret), DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        var completed = await TwoFactorTestHelper.CompleteSetupAsync(client, challenge.ChallengeToken, code);

        Assert.NotNull(completed.Session);
        Assert.Equal(10, completed.RecoveryCodes.Count);
        var repeat = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);
        Assert.Null(repeat.Secret);
    }

    [Fact]
    public async Task FiveWrongCodes_LockVerificationForFifteenMinutes()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await TwoFactorTestHelper.ConfigureTwoFactorAsync(factory, client, out var secret);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        for (var attempt = 0; attempt < 5; attempt++)
            await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, "000000");
        var correct = TotpCodeGenerator.Generate(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var afterLockout = await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, correct);

        Assert.Equal(HttpStatusCode.TooManyRequests, afterLockout.StatusCode);
    }

    [Fact]
    public async Task RecoveryCode_WorksOnceAndBurns()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var codes = await TwoFactorTestHelper.ConfigureTwoFactorAsync(factory, client, out _);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        var first = await TwoFactorTestHelper.VerifyAsync(client, challenge.ChallengeToken, codes[0]);
        var secondChallenge = await TwoFactorTestHelper.StartChallengeAsync(client);
        var second = await TwoFactorTestHelper.VerifyAsync(client, secondChallenge.ChallengeToken, codes[0]);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
    }

    [Fact]
    public async Task Reset_RequiresManagePermissionAndClearsSecret()
    {
        await using var factory = new PlatformApiFactory();
        using var supportClient = factory.CreateClient();
        var support = await PlatformAdminTestHelper.AuthorizeAsAsync(factory, supportClient,
            userName: "support@platform.test", roles: [PlatformAdminRoleNames.PlatformSupport]);
        using var adminClient = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, adminClient, roles: [PlatformAdminRoleNames.PlatformAdmin]);

        var denied = await supportClient.PostAsync($"/api/platform/admins/{support.PlatformAdminId:D}/2fa/reset", null);
        var allowed = await adminClient.PostAsync($"/api/platform/admins/{support.PlatformAdminId:D}/2fa/reset", null);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var user = await db.PlatformAdminUsers.SingleAsync(x => x.PlatformAdminUserId == support.PlatformAdminId);
        Assert.Null(user.TotpSecretEncrypted);
        Assert.Null(user.TotpEnabledAtUtc);
    }

    [Fact]
    public async Task SetupResponse_CarriesOtpAuthUriWithIssuer()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.SeedPlatformAdminAsync(factory, totpSecret: []);
        var challenge = await TwoFactorTestHelper.StartChallengeAsync(client);

        var setup = await TwoFactorTestHelper.BeginSetupAsync(client, challenge.ChallengeToken);

        Assert.StartsWith("otpauth://totp/AFK4", setup.OtpAuthUri);
        Assert.Contains("issuer=AFK4", setup.OtpAuthUri);
    }
}
```

Вспомогательный `TwoFactorTestHelper` создать рядом, в `tests/AFK4.Platform.Api.Tests/Platform/TwoFactorTestHelper.cs`: тонкие обёртки над четырьмя HTTP-маршрутами этой задачи (`StartChallengeAsync`, `BeginSetupAsync`, `CompleteSetupAsync`, `VerifyAsync`), метод `ConfigureTwoFactorAsync`, который проходит настройку до конца и возвращает коды восстановления, плюс `DecodeBase32`. Пустой `totpSecret: []` в `SeedPlatformAdminAsync` означает «2FA ещё не настроена» — этот случай нужен для сценариев настройки.

- [ ] **Step 2: Прогнать RED**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminTwoFactorTests`
Expected: ошибка компиляции — службы, эндпоинтов и хелпера нет.

- [ ] **Step 3: Реализовать службу и переключить вход**

Челлендж: 32 случайных байта, в базе `SHA256`, срок 2 минуты, `ConsumedAtUtc` ставится при успешной выдаче сессии. Секрет TOTP шифруется `ISecretProtector` (тот же, что у платёжных настроек). Коды восстановления — 10 строк по 10 символов, в базе только `SHA256`, в `RecoveryCodeHashesJson` массив hex-строк. `VerifyAsync`: сперва проверка `TwoFactorLockedUntilUtc`, затем TOTP с дрейфом ±1 шаг, затем перебор хэшей кодов восстановления с удалением использованного; при неудаче `FailedTwoFactorAttempts++`, на пятой — `TwoFactorLockedUntilUtc = now + 15 минут`; при успехе счётчик и блокировка сбрасываются, `LastSignInAtUtc = now`.

`PasswordHashingPlatformAdminCredentialService.SignInAsync` вместо `tokenService.IssueAsync(user, ...)` возвращает `twoFactorService.StartChallengeAsync(user, ...)`.

- [ ] **Step 4: Реализовать эндпоинты**

Маршруты 2FA живут вне обычной авторизации: их пускает challenge-токен, а не рабочая сессия, — как это уже сделано для анонимных маршрутов активации. Отображение ошибок: `InvalidChallenge` → 401, `InvalidCode` → 401, `LockedOut` → 429, `AlreadyConfigured` → 409. Сброс (`/2fa/reset`) идёт обычным путём под `ManagePlatformAdmins`. Каждый исход пишется в аудит (`PlatformAdminTwoFactorConfigured`, `PlatformAdminTwoFactorVerified`, `PlatformAdminTwoFactorReset`) с `Denied` на неудачах. Ни секрет, ни коды восстановления в `DetailsJson` не попадают. Регистрация в `Program.cs` рядом с `MapPlatformOrganizationEndpoints()`.

- [ ] **Step 5: Прогнать GREEN**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PlatformAdminTwoFactorTests`
Expected: PASS, шесть тестов.

- [ ] **Step 6: Починить `PlatformAdminTestHelper` — от него зависит весь остальной набор**

Это не побочная уборка, а обязательная часть задачи: `AuthorizeAsAsync` сейчас делает один запрос к `/api/platform/auth/sign-in` и ждёт `PlatformAdminSignInResponse`. После смены контракта он вернёт челлендж, и **каждый** платформенный тест станет красным.

Правки в `tests/AFK4.Platform.Api.Tests/Platform/PlatformAdminTestHelper.cs`:

- `SeedPlatformAdminAsync` получает необязательный параметр `byte[]? totpSecret = null`; когда он не задан, используется фиксированный тестовый секрет `System.Text.Encoding.ASCII.GetBytes("12345678901234567890")`. Секрет кладётся в `TotpSecretEncrypted` через `ISecretProtector` из `factory.Services`, `TotpEnabledAtUtc` заполняется. Так засеянный админ сразу «с настроенной 2FA», и существующим тестам не нужен экран настройки.
- `AuthorizeAsAsync` после первого шага берёт `ChallengeToken`, генерирует код `TotpCodeGenerator.Generate(secret, DateTimeOffset.UtcNow.ToUnixTimeSeconds())`, вызывает `/api/platform/auth/2fa/verify` и уже его ответ применяет как `Bearer`-токен. Сигнатура и возвращаемый тип не меняются — вызывающие тесты править не придётся.

- [ ] **Step 7: Прогнать весь платформенный набор — вход поменялся, соседи могли сломаться**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS. Если что-то ещё логинится паролем в обход хелпера — чинить через хелпер, а не ослаблением проверок.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Platform.Api src/AFK4.Shared.Contracts/Platform/Auth tests/AFK4.Platform.Api.Tests/Platform
git commit -m "feat(platform): двухшаговый вход с обязательным вторым фактором"
```

---

## Task 10: Фронт — двухшаговый вход и настройка 2FA

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/twoFactor.ts`
- Create: `src/AFK4.PlatformControl.Web/src/components/TwoFactorChallenge.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/components/TwoFactorSetup.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformTransport.ts:57-70`
- Modify: `src/AFK4.PlatformControl.Web/src/components/SignIn.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/settings/SettingsScreen.tsx`
- Modify: `src/AFK4.PlatformControl.Web/package.json` (+ `qrcode`, `@types/qrcode`)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `src/AFK4.PlatformControl.Web/src/components/TwoFactorChallenge.test.tsx`, `src/AFK4.PlatformControl.Web/src/api/platformTransport.test.ts`

**Interfaces:**
- Consumes: эндпоинты Task 9 (`/api/platform/auth/2fa/setup`, `/setup/confirm`, `/verify`, `/api/platform/admins/{id}/2fa/reset`), `SettingsScreen` из Task 7.
- Produces: `PlatformTransport.signIn(userName, password): Promise<SignInOutcome>`, где `type SignInOutcome = { kind: 'challenge'; challengeToken: string; twoFactorConfigured: boolean }`; и `PlatformTransport.completeTwoFactor(challengeToken: string, code: string): Promise<PlatformAdminSession>`. Сессия применяется к транспорту только на втором шаге.

- [ ] **Step 1: Написать падающие тесты**

```tsx
import { describe, expect, it } from 'bun:test';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { PlatformApiError } from '../api/platformApi';
import { TwoFactorChallenge } from './TwoFactorChallenge';

describe('TwoFactorChallenge', () => {
  it('отправляет введённый код и сообщает об успехе', async () => {
    let submitted = '';
    render(<TwoFactorChallenge
      onSubmit={async code => { submitted = code; }}
      onCancel={() => {}}
    />);

    await userEvent.type(screen.getByLabelText(/код/i), '123456');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(submitted).toBe('123456');
  });

  it('показывает понятную ошибку при блокировке', async () => {
    render(<TwoFactorChallenge
      onSubmit={async () => { throw new PlatformApiError(429, 'locked'); }}
      onCancel={() => {}}
    />);

    await userEvent.type(screen.getByLabelText(/код/i), '000000');
    await userEvent.click(screen.getByRole('button', { name: /подтвердить/i }));

    expect(await screen.findByText(/слишком много попыток/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Прогнать RED**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/components/TwoFactorChallenge.test.tsx`
Expected: FAIL — компонента нет.

- [ ] **Step 3: Реализовать поток**

`qrcode` уже используется в `OrganizationAdmin.Web` и `Player.Shell.Web` — это принятая проектная инфраструктура, ставим ту же версию (`^1.5.4`) и рисуем QR из `otpauth://`-ссылки, полученной от сервера, локально в canvas. Ставить зависимости: `"$BUN" install --force`.

`SignIn.tsx` после успешного пароля переключается на `TwoFactorChallenge` (если `twoFactorConfigured`) либо на `TwoFactorSetup`. `TwoFactorSetup` показывает QR, поле подтверждения и — после успеха — коды восстановления с кнопкой копирования и предупреждением, что показываются они один раз. В `SettingsScreen` добавить действие «Сбросить 2FA» с `ConfirmDialog`.

- [ ] **Step 4: Прогнать GREEN и сборку**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build`
Expected: тесты PASS, сборка чистая.

- [ ] **Step 5: Прогнать i18n-гарды и весь бэкенд**

Run: `cd packages/i18n && "$BUN" test` и `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS.

- [ ] **Step 6: Коммит**

```bash
git add src/AFK4.PlatformControl.Web locales packages/i18n/src
git commit -m "feat(platform-control): двухшаговый вход и настройка второго фактора"
```

---

## Ручная проверка перед merge

Автотесты не покрывают одно: реальное приложение-аутентификатор. Перед слиянием пройти руками:

1. Поднять панель, войти паролем — должен открыться экран настройки 2FA, а не панель.
2. Отсканировать QR любым аутентификатором, ввести код — панель открывается, коды восстановления показаны.
3. Перезайти — теперь спрашивается только код.
4. Пригласить второго сотрудника ролью поддержки, активировать код в приватном окне, войти под ним — раздела «Настройки» в рейле у него нет.
5. Попробовать отключить самого себя — кнопка заблокирована с пояснением.
