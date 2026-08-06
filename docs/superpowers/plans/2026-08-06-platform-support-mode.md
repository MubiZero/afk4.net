# Режим поддержки — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать платформенной поддержке временный вход под клиента: видеть то же, что видит клуб, править узкий список настроек и никогда не касаться денег.

**Architecture:** Грант доступа уже есть (`platform_support_access_grants`), но проверяется вручную внутри двух эндпоинтов — на сорок эндпоинтов такой путь не масштабируется. Вводим единый `PlatformSupportSessionMiddleware`: он аутентифицирует сессию поддержки по заголовку, требует на эндпоинте метку `PlatformSupportAccessMetadata` и строит синтетический `StaffContext` ровно с тем правом, которое объявлено в метке. Граница доступа тем самым живёт в разметке эндпоинтов, а не в наборе прав. Передача доступа из панели платформы в админку клиента — одноразовым билетом, который обменивается на сессионный токен.

**Tech Stack:** ASP.NET Core minimal APIs, EF Core 10 / Npgsql, xUnit + `PlatformApiFactory`; React 19 + TypeScript, `bun test` (happy-dom), `@afk4/i18n`.

## Global Constraints

- Спека: `docs/superpowers/specs/2026-08-04-platform-access-and-support-mode-design.md`. Это её слайсы 3 и 4.
- **Запись под режимом поддержки разрешена ровно в пяти файлах** и нигде больше: `BranchSettingsEndpoints`, `DeviceEndpoints`, `StaffEndpoints`, `FloorMapEndpoints`, `BranchProfileLayoutEndpoints`.
- **Никогда не помечать для записи**: `PosEndpoints`, `WalletEndpoints`, `MoneyActionEndpoints`, `ShiftEndpoints`, `PackageEndpoints`, `DcTopUpEndpoints`, `EskhataPaymentEndpoints`, `ShopOrderEndpoints`, `PlayerLoyaltyEndpoints`, `SessionEndpoints`, `ReservationEndpoints`, `TariffEndpoints`. Тарифы в запрете осознанно: цена для игроков — финансовое решение клуба.
- Заголовок сессии — существующая константа `PlatformSupportAccessGrantService.GrantHeaderName` = `"X-AFK4-Support-Access-Grant"`.
- Секреты (билет, сессионный токен) хранятся **только хэшами** (SHA-256), в ответе отдаются один раз и не логируются.
- i18n: ключи добавляются в `locales/ru.json`, `locales/en.json`, `locales/tg.json`, затем `cd packages/i18n && bun run gen`. Таджикский — настоящий перевод, не копия русского (есть guard-тест).
- Фронтовые слайсы завершаются `bun run build` в затронутом приложении: `tsc -b` типизирует и тесты, зелёный `bun test` сам по себе ничего не доказывает.
- Бэкенд: `dotnet test tests/AFK4.Platform.Api.Tests`. Postgres-тесты требуют переменных из `.github/workflows/pr-verification.yml`; локально поднимается `docker run -d --rm --name afk4-pgtest -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=afk4_ci_test -p 55432:5432 postgres:16`.
- Ветка базируется на `design/platform-control-shared-ui`.

## Принятые решения (в спеке не детализированы)

1. **Единый middleware вместо ручных проверок.** Сегодня `DiagnosticsEndpoints.cs:94-102` и `OrganizationAuditEndpoints.cs:74-82` вручную зовут `ValidateAsync`. Эти два места переводятся на общий механизм и ручные ветвления удаляются.
2. **Ломающее изменение.** Старый путь «платформенный access-токен + `grantId` в заголовке» заменяется на «сессионный токен поддержки». Во фронтах гранты сейчас не используются нигде (0 упоминаний), внешних потребителей нет — совместимость не поддерживаем.
3. **Права выдаются точечно.** Синтетический `StaffContext` получает ровно `metadata.Permission` текущего эндпоинта, а не каталог роли владельца. Полный каталог дал бы поддержке денежные права, и единственной защитой осталась бы разметка.
4. **Приостановленная организация остаётся недоступной** — `OrganizationSuspensionMiddleware` не трогаем. Если поддержке понадобится входить в приостановленный клуб, это отдельное решение с отдельным аудитом.
5. **URL админки клиента** живёт в конфигурации API (`SupportAccess:OrganizationAdminBaseUrl`), а не во фронте: адрес один на среду, и он нужен серверу, чтобы собрать ссылку с билетом.

---

## Структура файлов

**Создаются (сервер):**
- `src/AFK4.Platform.Api/Platform/Support/PlatformSupportSessionMiddleware.cs` — аутентификация сессии, проверка метки эндпоинта, сборка `StaffContext`.
- `src/AFK4.Platform.Api/Platform/Support/IPlatformSupportContextAccessor.cs` + `PlatformSupportContextAccessor.cs` — доступ к контексту поддержки из аудита.
- `src/AFK4.Platform.Api/Configuration/SupportAccessOptions.cs` — базовый URL админки клиента.
- `src/AFK4.Platform.Api/Endpoints/SupportAccessSessionEndpoints.cs` — обмен билета, текущая сессия, выход.

**Изменяются (сервер):**
- `PlatformSupportAccessGrantEntity.cs`, `PlatformDbContext.cs` — билет и сессия.
- `PlatformSupportAccessGrantService.cs` — выпуск билета, обмен, аутентификация сессии.
- `StaffContext.cs` — поле `SupportAccess`.
- `AuditRecordStager.cs` — актор аудита под поддержкой.
- `Program.cs` — DI, middleware, регистрация эндпоинтов.
- 5 файлов белого списка + все org-эндпоинты с GET — разметка.
- `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs` — страж белого списка.

**Создаются (фронт):**
- `src/AFK4.PlatformControl.Web/src/api/platformClients/supportAccess.ts`
- `src/AFK4.PlatformControl.Web/src/platform/organizations/SupportAccessSection.tsx`
- `src/AFK4.OrganizationAdmin.Web/src/support/supportSession.ts` — хранение сессии.
- `src/AFK4.OrganizationAdmin.Web/src/support/SupportModeBanner.tsx` — плашка.

---

### Task 1: Билет и сессия в сущности гранта

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PlatformSupportAccessGrantEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs:983-990`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessPersistenceTests.cs` (создать)

**Interfaces:**
- Produces: поля `TicketHash`, `TicketUsedAtUtc`, `SessionTokenHash` на `PlatformSupportAccessGrantEntity`.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessPersistenceTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessPersistenceTests
{
    [Fact]
    public async Task Grant_RoundTripsTicketAndSessionHashes()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        var grantId = Guid.NewGuid();
        db.PlatformSupportAccessGrants.Add(new PlatformSupportAccessGrantEntity
        {
            GrantId = grantId,
            PlatformAdminUserId = Guid.NewGuid(),
            OrganizationId = Guid.NewGuid(),
            Reason = "Клуб сообщает, что не открывается смена",
            IssuedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddMinutes(30),
            TicketHash = [1, 2, 3],
            SessionTokenHash = [4, 5, 6]
        });
        await db.SaveChangesAsync();

        var stored = await db.PlatformSupportAccessGrants.SingleAsync(g => g.GrantId == grantId);

        Assert.Equal(new byte[] { 1, 2, 3 }, stored.TicketHash);
        Assert.Equal(new byte[] { 4, 5, 6 }, stored.SessionTokenHash);
        Assert.Null(stored.TicketUsedAtUtc);
    }
}
```

- [ ] **Step 2: Убедиться, что тест не компилируется**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportAccessPersistenceTests"`
Expected: ошибка компиляции — `TicketHash` не существует.

- [ ] **Step 3: Добавить поля в сущность**

В `src/AFK4.Platform.Api/Data/PlatformSupportAccessGrantEntity.cs` после `RevokedAtUtc`:

```csharp
    // Одноразовый билет: панель платформы отдаёт его в ссылке, админка клиента меняет на сессию.
    public byte[]? TicketHash { get; set; }

    public DateTimeOffset? TicketUsedAtUtc { get; set; }

    // Токен сессии поддержки; живёт ровно до конца гранта.
    public byte[]? SessionTokenHash { get; set; }
```

- [ ] **Step 4: Настроить индексы**

В `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` в блоке конфигурации `platform_support_access_grants` (после существующих индексов на строке 989) добавить:

```csharp
            entity.HasIndex(grant => grant.TicketHash).IsUnique().HasFilter("\"TicketHash\" IS NOT NULL");
            entity.HasIndex(grant => grant.SessionTokenHash).IsUnique().HasFilter("\"SessionTokenHash\" IS NOT NULL");
```

- [ ] **Step 5: Создать миграцию**

Сначала собрать проект — иначе `dotnet ef` возьмёт устаревшую модель и выдаст пустую миграцию:

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddSupportAccessTicketAndSession \
  --project src/AFK4.Platform.Api --output-dir Data/Migrations --no-build
```

Открыть созданный файл и убедиться, что `Up` не пуст и добавляет три колонки и два индекса.

- [ ] **Step 6: Прогнать тест**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportAccessPersistenceTests"`
Expected: PASS

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(platform): билет и сессия поддержки в гранте доступа"
```

---

### Task 2: Выпуск билета и обмен на сессию

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Support/PlatformSupportAccessGrantService.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Support/PlatformSupportAccessContracts.cs`
- Create: `src/AFK4.Platform.Api/Configuration/SupportAccessOptions.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessTicketTests.cs` (создать)

**Interfaces:**
- Consumes: поля из Task 1.
- Produces:
  - `record PlatformSupportAccessGrantIssue(PlatformSupportAccessGrantDto Grant, string Ticket, string AdminUrl)`
  - `record PlatformSupportSessionDto(string SessionToken, Guid OrganizationId, string OrganizationName, string Reason, DateTimeOffset ExpiresAtUtc, IReadOnlyList<string> WritableAreas)`
  - `Task<PlatformSupportAccessGrantIssue?> IssueAsync(Guid platformAdminUserId, CreatePlatformSupportAccessGrantRequest request, CancellationToken cancellationToken)`
  - `Task<PlatformSupportSessionDto?> RedeemTicketAsync(string ticket, CancellationToken cancellationToken)`
  - `Task<PlatformSupportContext?> AuthenticateSessionAsync(string sessionToken, string requiredPermission, CancellationToken cancellationToken)`

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportAccessTicketTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportAccessTicketTests
{
    [Fact]
    public async Task RedeemTicket_Twice_SucceedsOnlyOnce()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            Guid.NewGuid(),
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Смена не открывается у клуба", 30),
            CancellationToken.None);

        Assert.NotNull(issue);

        var first = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);
        var second = await service.RedeemTicketAsync(issue.Ticket, CancellationToken.None);

        Assert.NotNull(first);
        Assert.False(string.IsNullOrWhiteSpace(first!.SessionToken));
        Assert.Null(second);
    }

    [Fact]
    public async Task AuthenticateSession_AfterRevocation_Fails()
    {
        await using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<PlatformSupportAccessGrantService>();

        var adminId = Guid.NewGuid();
        var organizationId = await SeedOrganizationAsync(db);
        var issue = await service.IssueAsync(
            adminId,
            new CreatePlatformSupportAccessGrantRequest(organizationId, "Устройство не видно в списке", 30),
            CancellationToken.None);
        var session = await service.RedeemTicketAsync(issue!.Ticket, CancellationToken.None);

        var before = await service.AuthenticateSessionAsync(
            session!.SessionToken, "organization.branch_settings.manage", CancellationToken.None);
        await service.RevokeAsync(issue.Grant.GrantId, adminId, CancellationToken.None);
        var after = await service.AuthenticateSessionAsync(
            session.SessionToken, "organization.branch_settings.manage", CancellationToken.None);

        Assert.NotNull(before);
        Assert.Null(after);
    }

    private static async Task<Guid> SeedOrganizationAsync(PlatformDbContext db)
    {
        var organizationId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = organizationId,
            Slug = $"club-{organizationId:N}",
            Name = "Тестовый клуб",
            Status = "active",
            PlanCode = "starter",
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return organizationId;
    }
}
```

Если поля `OrganizationEntity` в `SeedOrganizationAsync` не совпадут с текущими обязательными — привести вызов в соответствие с `src/AFK4.Platform.Api/Data/OrganizationEntity.cs`, не меняя смысла теста.

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportAccessTicketTests"`
Expected: ошибка компиляции — `IssueAsync` не существует.

- [ ] **Step 3: Добавить конфигурацию**

Создать `src/AFK4.Platform.Api/Configuration/SupportAccessOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Configuration;

public sealed class SupportAccessOptions
{
    public const string SectionName = "SupportAccess";

    /// <summary>
    /// Адрес админки клиента для входа под клиента. Один на среду: панель платформы сама
    /// его не знает, а держать адрес в двух местах — верный способ развести их со временем.
    /// </summary>
    public string OrganizationAdminBaseUrl { get; set; } = string.Empty;
}
```

В `src/AFK4.Platform.Api/appsettings.json` добавить секцию верхнего уровня:

```json
  "SupportAccess": {
    "OrganizationAdminBaseUrl": ""
  },
```

- [ ] **Step 4: Расширить контракты**

В `src/AFK4.Shared.Contracts/Platform/Support/PlatformSupportAccessContracts.cs` добавить:

```csharp
public sealed record PlatformSupportAccessGrantIssue(
    PlatformSupportAccessGrantDto Grant,
    string Ticket,
    string AdminUrl);

public sealed record PlatformSupportSessionDto(
    string SessionToken,
    Guid OrganizationId,
    string OrganizationName,
    string Reason,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> WritableAreas);

public sealed record RedeemSupportAccessTicketRequest(string Ticket);
```

- [ ] **Step 5: Реализовать выпуск, обмен и аутентификацию**

В `PlatformSupportAccessGrantService.cs` добавить зависимость `IOptions<SupportAccessOptions> supportAccessOptions` в конструктор и методы:

```csharp
    // Билет живёт 60 секунд: он нужен ровно на переход между двумя вкладками.
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromSeconds(60);

    public async Task<PlatformSupportAccessGrantIssue?> IssueAsync(
        Guid platformAdminUserId,
        CreatePlatformSupportAccessGrantRequest request,
        CancellationToken cancellationToken)
    {
        var grant = await CreateAsync(platformAdminUserId, request, cancellationToken);
        if (grant is null)
        {
            return null;
        }

        var ticket = GenerateSecret();
        var entity = await dbContext.PlatformSupportAccessGrants
            .SingleAsync(candidate => candidate.GrantId == grant.GrantId, cancellationToken);
        entity.TicketHash = Hash(ticket);
        await dbContext.SaveChangesAsync(cancellationToken);

        var baseUrl = supportAccessOptions.Value.OrganizationAdminBaseUrl.TrimEnd('/');
        return new PlatformSupportAccessGrantIssue(grant, ticket, $"{baseUrl}/support-access?ticket={ticket}");
    }

    public async Task<PlatformSupportSessionDto?> RedeemTicketAsync(
        string ticket,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(ticket))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var ticketHash = Hash(ticket);
        var grant = await dbContext.PlatformSupportAccessGrants
            .SingleOrDefaultAsync(
                candidate => candidate.TicketHash == ticketHash
                    && candidate.TicketUsedAtUtc == null
                    && candidate.RevokedAtUtc == null
                    && candidate.ExpiresAtUtc > now,
                cancellationToken);

        if (grant is null || grant.IssuedAtUtc + TicketLifetime < now)
        {
            return null;
        }

        var sessionToken = GenerateSecret();
        grant.TicketUsedAtUtc = now;
        grant.SessionTokenHash = Hash(sessionToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        var organization = await dbContext.Organizations
            .AsNoTracking()
            .SingleAsync(candidate => candidate.OrganizationId == grant.OrganizationId, cancellationToken);

        return new PlatformSupportSessionDto(
            sessionToken,
            grant.OrganizationId,
            organization.Name,
            grant.Reason,
            grant.ExpiresAtUtc,
            PlatformSupportWritableAreas.All);
    }

    public async Task<PlatformSupportContext?> AuthenticateSessionAsync(
        string sessionToken,
        string requiredPermission,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return null;
        }

        var now = timeProvider.GetUtcNow();
        var sessionHash = Hash(sessionToken);
        var grant = await dbContext.PlatformSupportAccessGrants
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.SessionTokenHash == sessionHash
                    && candidate.RevokedAtUtc == null
                    && candidate.ExpiresAtUtc > now,
                cancellationToken);

        return grant is null
            ? null
            : new PlatformSupportContext(
                grant.GrantId,
                grant.PlatformAdminUserId,
                grant.OrganizationId,
                grant.Reason,
                requiredPermission,
                grant.ExpiresAtUtc);
    }

    private static string GenerateSecret() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

    private static byte[] Hash(string secret) =>
        SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(secret));
```

Добавить `using System.Security.Cryptography;`, `using Microsoft.Extensions.Options;`, `using AFK4.Platform.Api.Configuration;`.

- [ ] **Step 6: Объявить области записи**

Создать `src/AFK4.Platform.Api/Platform/Support/PlatformSupportWritableAreas.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Support;

/// <summary>
/// Что поддержка может менять под грантом. Список отдаётся админке клиента, чтобы она гасила
/// недоступное заранее: кнопка, которая всегда возвращает 403, читается как поломка продукта.
/// </summary>
public static class PlatformSupportWritableAreas
{
    public const string BranchSettings = "branch-settings";
    public const string Devices = "devices";
    public const string Staff = "staff";
    public const string FloorMap = "floor-map";
    public const string BranchProfile = "branch-profile";

    public static readonly IReadOnlyList<string> All =
        [BranchSettings, Devices, Staff, FloorMap, BranchProfile];
}
```

- [ ] **Step 7: Зарегистрировать конфигурацию**

В `src/AFK4.Platform.Api/Program.cs` рядом с прочими `Configure<...>` добавить:

```csharp
builder.Services.Configure<SupportAccessOptions>(
    builder.Configuration.GetSection(SupportAccessOptions.SectionName));
```

- [ ] **Step 8: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportAccessTicketTests"`
Expected: PASS (2 теста)

- [ ] **Step 9: Коммит**

```bash
git add src tests
git commit -m "feat(platform): одноразовый билет и сессия поддержки"
```

---

### Task 3: Middleware сессии поддержки

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Support/PlatformSupportContextAccessor.cs`
- Create: `src/AFK4.Platform.Api/Platform/Support/PlatformSupportSessionMiddleware.cs`
- Modify: `src/AFK4.Platform.Api/Identity/StaffContext.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportSessionMiddlewareTests.cs` (создать)

**Interfaces:**
- Consumes: `AuthenticateSessionAsync` из Task 2.
- Produces:
  - `interface IPlatformSupportContextAccessor { PlatformSupportContext? Current { get; set; } }`
  - `StaffContext.SupportAccess` — `PlatformSupportContext?`, init-поле.

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/AFK4.Platform.Api.Tests/Platform/PlatformSupportSessionMiddlewareTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformSupportSessionMiddlewareTests
{
    [Fact]
    public async Task UnmarkedEndpoint_WithValidSession_IsForbidden()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        // Смены денежные и никогда не помечаются для поддержки.
        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/shifts/current");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task MarkedReadEndpoint_WithValidSession_Succeeds()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task MarkedEndpoint_WithUnknownSession_IsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (_, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, "не-существующий-токен");

        var response = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

Создать хелпер `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessTestHelper.cs`, который сеет организацию с одним филиалом, выпускает грант через `PlatformSupportAccessGrantService.IssueAsync`, меняет билет через `RedeemTicketAsync` и возвращает `(string SessionToken, Guid OrganizationId, Guid BranchId)`. Организацию и филиал сеять напрямую через `PlatformDbContext`, повторяя набор обязательных полей из `OrganizationEntity` и `BranchEntity`.

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportSessionMiddlewareTests"`
Expected: FAIL — сейчас запрос вернёт 401, а не ожидаемые статусы (эндпоинт не помечен и middleware отсутствует).

- [ ] **Step 3: Добавить accessor**

Создать `src/AFK4.Platform.Api/Platform/Support/PlatformSupportContextAccessor.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Support;

public interface IPlatformSupportContextAccessor
{
    PlatformSupportContext? Current { get; set; }
}

public sealed class PlatformSupportContextAccessor : IPlatformSupportContextAccessor
{
    public PlatformSupportContext? Current { get; set; }
}
```

- [ ] **Step 4: Добавить поле в StaffContext**

В `src/AFK4.Platform.Api/Identity/StaffContext.cs` после `PermissionsByBranch` добавить:

```csharp
    // Заполнено, когда за сотрудника клуба действует платформенная поддержка под грантом.
    // Аудит по этому полю пишет платформенного администратора вместо сотрудника.
    public PlatformSupportContext? SupportAccess { get; init; }
```

Добавить `using AFK4.Platform.Api.Platform.Support;`.

- [ ] **Step 5: Реализовать middleware**

Создать `src/AFK4.Platform.Api/Platform/Support/PlatformSupportSessionMiddleware.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Support;

/// <summary>
/// Пускает платформенную поддержку в админку клиента по сессионному токену. Граница доступа —
/// метка <see cref="PlatformSupportAccessMetadata"/> на эндпоинте: без неё сессия не проходит,
/// поэтому денежные эндпоинты закрыты уже тем, что их никто не помечал.
/// </summary>
public sealed class PlatformSupportSessionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        PlatformSupportAccessGrantService supportAccessService,
        IStaffContextAccessor staffContextAccessor,
        IPlatformSupportContextAccessor supportContextAccessor,
        PlatformDbContext dbContext)
    {
        var header = context.Request.Headers[PlatformSupportAccessGrantService.GrantHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(header))
        {
            await next(context);
            return;
        }

        var metadata = context.GetEndpoint()?.Metadata.GetMetadata<PlatformSupportAccessMetadata>();
        if (metadata is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        var support = await supportAccessService.AuthenticateSessionAsync(
            header, metadata.Permission, context.RequestAborted);

        if (support is null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return;
        }

        var branchIds = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.OrganizationId == support.OrganizationId)
            .Select(branch => branch.BranchId)
            .ToListAsync(context.RequestAborted);

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { metadata.Permission };

        supportContextAccessor.Current = support;
        staffContextAccessor.Current = new StaffContext(
            StaffUserId: Guid.Empty,
            OrganizationId: support.OrganizationId,
            DisplayName: "Поддержка платформы",
            BranchIds: branchIds.ToHashSet(),
            Permissions: permissions)
        {
            SupportAccess = support,
            PermissionsByBranch = branchIds.ToDictionary(
                branchId => branchId,
                _ => (IReadOnlySet<string>)permissions)
        };

        await next(context);
    }
}
```

- [ ] **Step 6: Зарегистрировать**

В `src/AFK4.Platform.Api/Program.cs` рядом с `AddScoped<PlatformSupportAccessGrantService>()` (строка 190) добавить:

```csharp
builder.Services.AddScoped<IPlatformSupportContextAccessor, PlatformSupportContextAccessor>();
```

И в конвейере — сразу после `app.UseMiddleware<PlayerAuthenticationMiddleware>();` (строка 421), до проверки домена:

```csharp
app.UseMiddleware<PlatformSupportSessionMiddleware>();
```

- [ ] **Step 7: Пометить эндпоинт настроек филиала**

В `src/AFK4.Platform.Api/Endpoints/BranchSettingsEndpoints.cs` к `MapGet("branches/{branchId:guid}/settings", ...)` дописать:

```csharp
    .AllowPlatformSupportAccess(OrganizationPermissionNames.ManageBranchSettings);
```

- [ ] **Step 8: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PlatformSupportSessionMiddlewareTests"`
Expected: PASS (3 теста)

- [ ] **Step 9: Доказать, что проверка метки не декоративна**

Временно убрать в middleware ветку `metadata is null` (пропускать дальше), прогнать тесты, убедиться, что `UnmarkedEndpoint_WithValidSession_IsForbidden` падает. Вернуть ветку.

- [ ] **Step 10: Коммит**

```bash
git add src tests
git commit -m "feat(platform): единая аутентификация сессии поддержки"
```

---

### Task 4: Аудит под поддержкой пишет платформенного администратора

**Files:**
- Modify: `src/AFK4.Platform.Api/Audit/AuditRecordStager.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessAuditTests.cs` (создать)

**Interfaces:**
- Consumes: `StaffContext.SupportAccess` из Task 3.

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessAuditTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessAuditTests
{
    [Fact]
    public async Task ReadUnderSupport_RecordsPlatformAdminAsActor()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);

        client.DefaultRequestHeaders.Add(
            PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);
        await client.GetAsync($"/api/organizations/{organizationId}/branches/{branchId}/settings");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords
            .OrderByDescending(candidate => candidate.CreatedAtUtc)
            .FirstAsync(candidate => candidate.OrganizationId == organizationId);

        Assert.NotNull(record.ActorPlatformAdminUserId);
        Assert.Null(record.ActorStaffUserId);
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~SupportAccessAuditTests"`
Expected: FAIL — актором записан сотрудник (`Guid.Empty`), а не платформенный администратор.

- [ ] **Step 3: Переписать актора в стейджере**

В `src/AFK4.Platform.Api/Audit/AuditRecordStager.cs` внедрить `IStaffContextAccessor staffContextAccessor` и в `Stage` перед созданием записи:

```csharp
        var support = staffContextAccessor.Current?.SupportAccess;
        if (support is not null)
        {
            // Под грантом действует платформенный сотрудник; записать сотрудника клуба означало бы
            // приписать клубу чужое действие.
            request = request with
            {
                ActorStaffUserId = null,
                ActorPlatformAdminUserId = support.PlatformAdminUserId
            };
        }
```

- [ ] **Step 4: Прогнать тест**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~SupportAccessAuditTests"`
Expected: PASS

- [ ] **Step 5: Прогнать весь набор аудита**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~Audit"`
Expected: PASS — обычный путь сотрудника не задет.

- [ ] **Step 6: Коммит**

```bash
git add src tests
git commit -m "feat(platform): аудит под поддержкой указывает платформенного администратора"
```

---

### Task 5: Разметка эндпоинтов чтения

**Files:**
- Modify: все org-эндпоинты с GET-маршрутами (см. список ниже)
- Test: `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs`

**Interfaces:**
- Consumes: `.AllowPlatformSupportAccess(permission)` из `EndpointAuthenticationDomainExtensions.cs:17`.

- [ ] **Step 1: Переписать страж белого списка**

В `tests/AFK4.Platform.Api.Tests/Architecture/AuthenticationDomainEndpointTests.cs` заменить тест `PlatformSupportAllowlist_ContainsOnlyReadOnlyOrganizationEndpoints` на:

```csharp
    // Запись под режимом поддержки разрешена ровно в этих областях. Список намеренно
    // дублирует спеку: если кто-то пометит денежный эндпоинт «за компанию», тест назовёт его
    // поимённо, а не промолчит.
    private static readonly HashSet<string> WritableRoutePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/settings",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/floor-map",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/profile",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/layout",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/staff",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/devices",
        "/api/organizations/{organizationId:guid}/devices",
        "/api/organizations/{organizationId:guid}/branches/{branchId:guid}/device-enrollment-codes"
    };

    [Fact]
    public void PlatformSupportAllowlist_AllowsWritesOnlyInDeclaredAreas()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Where(endpoint => endpoint.Metadata.GetMetadata<HttpMethodMetadata>() is { } methods
                && !methods.HttpMethods.Contains(HttpMethods.Get))
            .Where(endpoint => !WritableRoutePrefixes.Any(prefix =>
                endpoint.RoutePattern.RawText!.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "Запись под режимом поддержки разрешена только в объявленных областях, "
                + $"а помечены ещё и эти: {string.Join(", ", offenders)}");
    }

    [Fact]
    public void PlatformSupportAllowlist_NeverCoversMoneyEndpoints()
    {
        using var factory = new PlatformApiFactory();
        using var _ = factory.CreateClient();
        var dataSource = factory.Services.GetRequiredService<EndpointDataSource>();

        string[] forbidden =
        [
            "/pos/", "/wallet/", "/money-actions", "/shifts", "/packages",
            "/dc-topups", "/payments/eskhata", "/shop/orders", "/loyalty",
            "/sessions", "/reservations", "/tariffs"
        ];

        var offenders = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<PlatformSupportAccessMetadata>() is not null)
            .Select(endpoint => endpoint.RoutePattern.RawText!)
            .Where(route => forbidden.Any(fragment => route.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            $"Денежные эндпоинты не открываются поддержке ни на чтение, ни на запись: {string.Join(", ", offenders)}");
    }
```

- [ ] **Step 2: Прогнать — тесты должны пройти на текущей разметке**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~AuthenticationDomainEndpointTests"`
Expected: PASS

- [ ] **Step 3: Пометить GET-эндпоинты чтения**

Дописать `.AllowPlatformSupportAccess(<право этого эндпоинта>)` к каждому GET-маршруту в файлах: `DashboardEndpoints`, `DeviceEndpoints` (6 GET), `StaffEndpoints` (1 GET), `FloorMapEndpoints` (1 GET), `BranchProfileLayoutEndpoints` (2 GET), `OrganizationAdminReportEndpoints` (5 GET), `ReportEndpoints` (11 GET), `NewsEndpoints` (2 GET), `LoyaltySettingsEndpoints` (1 GET), `DcConfigEndpoints` (1 GET), `EskhataConfigEndpoints` (1 GET), `ReportScheduleEndpoints` (1 GET), `UpdateEndpoints` (org-GET), `BranchSettingsEndpoints` (уже помечен в Task 3).

Право берётся то же, что уже требует сам эндпоинт в `RequireBranchPermissionAsync`/`RequireOrganizationPermissionAsync` — не выдумывать новых.

**Денежные файлы из списка запрета не трогать вообще**, даже их GET-маршруты: спека открывает поддержке чтение, но денежные эндпоинты названы поимённо как исключённые.

- [ ] **Step 4: Прогнать страж**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~AuthenticationDomainEndpointTests"`
Expected: PASS

- [ ] **Step 5: Прогнать весь набор**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS

- [ ] **Step 6: Коммит**

```bash
git add src tests
git commit -m "feat(platform): открыть поддержке чтение экранов клуба"
```

---

### Task 6: Разметка разрешённых записей

**Files:**
- Modify: `BranchSettingsEndpoints.cs`, `DeviceEndpoints.cs`, `StaffEndpoints.cs`, `FloorMapEndpoints.cs`, `BranchProfileLayoutEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessWriteTests.cs` (создать)

- [ ] **Step 1: Написать тесты границы**

Создать `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessWriteTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Organizations;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessWriteTests
{
    [Fact]
    public async Task BranchSettings_AreWritableUnderSupport()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.PutAsJsonAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings",
            new UpdateBranchSettingsRequest(organizationId, true, "ru"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("pos/sales")]
    [InlineData("tariffs")]
    public async Task MoneyEndpoints_StayClosedUnderSupport(string suffix)
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var response = await client.PostAsJsonAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/{suffix}",
            new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

Тип `UpdateBranchSettingsRequest` и его поля сверить с `src/AFK4.Shared.Contracts`, не выдумывать.

- [ ] **Step 2: Убедиться, что первый тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~SupportAccessWriteTests"`
Expected: `BranchSettings_AreWritableUnderSupport` → 403 (PUT ещё не помечен), денежные → уже PASS.

- [ ] **Step 3: Пометить записи белого списка**

Дописать `.AllowPlatformSupportAccess(<право эндпоинта>)` ко ВСЕМ не-GET маршрутам пяти файлов:
- `BranchSettingsEndpoints`: PUT settings.
- `FloorMapEndpoints`: PUT floor-map.
- `BranchProfileLayoutEndpoints`: PATCH profile, POST/PATCH/DELETE zones, POST/PATCH/DELETE seats (7 маршрутов).
- `StaffEndpoints`: PATCH roles/profile/state, POST password-reset (4 маршрута).
- `DeviceEndpoints`: только org-scoped маршруты управления устройствами (approve, reject, rename, move-seat, remove, seat-assignment, commands, credentials/rotate, credentials/revoke, device-enrollment-codes). **Маршруты самого устройства не трогать** (`/api/devices/enroll`, `heartbeat`, `commands/{id}/result`, `session-reconciliation`, `installed-apps/report`, `install/auth/*`) — они принадлежат домену устройства, а не клуба, и поддержке там делать нечего.

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~SupportAccessWriteTests"`
Expected: PASS

- [ ] **Step 5: Прогнать страж белого списка**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~AuthenticationDomainEndpointTests"`
Expected: PASS. Если страж ругается на маршрут — значит он вне объявленных областей: не расширять список молча, а проверить, действительно ли этот маршрут разрешён спекой.

- [ ] **Step 6: Коммит**

```bash
git add src tests
git commit -m "feat(platform): разрешить поддержке правки настроек, устройств, персонала и карты"
```

---

### Task 7: Эндпоинты сессии поддержки и перевод двух старых на общий механизм

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/SupportAccessSessionEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformSupportAccessEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/DiagnosticsEndpoints.cs:89-144`
- Modify: `src/AFK4.Platform.Api/Endpoints/OrganizationAuditEndpoints.cs:73-104`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessSessionEndpointTests.cs` (создать)

**Interfaces:**
- Produces: `POST /api/public/support-access/sessions`, `GET /api/support-access/session`, `DELETE /api/support-access/session`.

- [ ] **Step 1: Написать падающие тесты**

Создать `tests/AFK4.Platform.Api.Tests/Platform/SupportAccessSessionEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SupportAccessSessionEndpointTests
{
    [Fact]
    public async Task RedeemTicket_ReturnsSessionOnce()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var ticket = await SupportAccessTestHelper.IssueTicketAsync(factory);

        var first = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(ticket));
        var second = await client.PostAsJsonAsync(
            "/api/public/support-access/sessions", new RedeemSupportAccessTicketRequest(ticket));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);

        var session = await first.Content.ReadFromJsonAsync<PlatformSupportSessionDto>();
        Assert.NotNull(session);
        Assert.Contains(PlatformSupportWritableAreas.BranchSettings, session!.WritableAreas);
    }

    [Fact]
    public async Task SignOut_EndsTheSession()
    {
        await using var factory = new PlatformApiFactory();
        var client = factory.CreateClient();
        var (sessionToken, organizationId, branchId) = await SupportAccessTestHelper.OpenSessionAsync(factory);
        client.DefaultRequestHeaders.Add(PlatformSupportAccessGrantService.GrantHeaderName, sessionToken);

        var signOut = await client.DeleteAsync("/api/support-access/session");
        var afterSignOut = await client.GetAsync(
            $"/api/organizations/{organizationId}/branches/{branchId}/settings");

        Assert.Equal(HttpStatusCode.NoContent, signOut.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterSignOut.StatusCode);
    }
}
```

- [ ] **Step 2: Убедиться, что тесты падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~SupportAccessSessionEndpointTests"`
Expected: FAIL — 404, эндпоинтов нет.

- [ ] **Step 3: Реализовать эндпоинты**

Создать `src/AFK4.Platform.Api/Endpoints/SupportAccessSessionEndpoints.cs`:

```csharp
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Platform.Support;
using AFK4.Shared.Contracts.Platform.Support;

namespace AFK4.Platform.Api.Endpoints;

public static class SupportAccessSessionEndpoints
{
    public static void MapSupportAccessSessionEndpoints(this WebApplication app)
    {
        // Публичный: у админки клиента на этом шаге ещё нет ничего, кроме билета.
        app.MapPost("/api/public/support-access/sessions", async (
            RedeemSupportAccessTicketRequest request,
            PlatformSupportAccessGrantService supportAccessService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var session = await supportAccessService.RedeemTicketAsync(request.Ticket, cancellationToken);
            if (session is null)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            return Results.Ok(session);
        });

        app.MapDelete("/api/support-access/session", async (
            HttpContext context,
            PlatformSupportAccessGrantService supportAccessService,
            IPlatformSupportContextAccessor supportContextAccessor,
            CancellationToken cancellationToken) =>
        {
            var support = supportContextAccessor.Current;
            if (support is null)
            {
                return Results.Unauthorized();
            }

            await supportAccessService.RevokeAsync(
                support.GrantId, support.PlatformAdminUserId, cancellationToken);
            return Results.NoContent();
        }).AllowPlatformSupportAccess(PlatformSupportSelfPermission);

        app.MapGet("/api/support-access/session", (
            IPlatformSupportContextAccessor supportContextAccessor) =>
        {
            var support = supportContextAccessor.Current;
            return support is null ? Results.Unauthorized() : Results.Ok(support);
        }).AllowPlatformSupportAccess(PlatformSupportSelfPermission);
    }

    // Собственные эндпоинты сессии не требуют прав клуба: это управление самой сессией.
    private const string PlatformSupportSelfPermission = "organization.support_access.self";
}
```

- [ ] **Step 4: Зарегистрировать**

В `src/AFK4.Platform.Api/Program.cs` рядом с прочими `app.Map*Endpoints()` добавить `app.MapSupportAccessSessionEndpoints();`.

- [ ] **Step 5: Перевести два старых эндпоинта на общий механизм**

В `DiagnosticsEndpoints.cs` и `OrganizationAuditEndpoints.cs` удалить ручные блоки с `ValidateBranchAsync`/`ValidateAsync` и ветвления `support is null ? ... : ...`: теперь `StaffContext` уже построен middleware, и обычный путь через `RequireBranchPermissionAsync` работает для поддержки без изменений. Метки `.AllowPlatformSupportAccess(...)` на этих эндпоинтах оставить.

Удалить ставшие ненужными `ValidateAsync`/`ValidateBranchAsync` из `PlatformSupportAccessGrantService`, если после этой правки на них не осталось вызовов.

- [ ] **Step 6: Отдавать билет при создании гранта**

В `PlatformSupportAccessEndpoints.cs` заменить вызов `CreateAsync` на `IssueAsync` и вернуть `PlatformSupportAccessGrantIssue`.

- [ ] **Step 7: Прогнать весь набор**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS

- [ ] **Step 8: Коммит**

```bash
git add src tests
git commit -m "feat(platform): эндпоинты сессии поддержки, единый механизм для диагностики и журнала"
```

---

### Task 8: Панель платформы — выдача доступа

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/supportAccess.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/SupportAccessSection.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/SupportAccessSection.test.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.tsx:109`
- Modify: `locales/{ru,en,tg}.json`

**Interfaces:**
- Consumes: `POST /api/platform/support-access-grants` → `PlatformSupportAccessGrantIssue`.

- [ ] **Step 1: Добавить типы и сабклиент**

В `src/AFK4.PlatformControl.Web/src/api/types.ts`:

```ts
export interface SupportAccessGrant {
  grantId: string;
  organizationId: string;
  reason: string;
  issuedAtUtc: string;
  expiresAtUtc: string;
  revokedAtUtc: string | null;
}

export interface SupportAccessGrantIssue {
  grant: SupportAccessGrant;
  ticket: string;
  adminUrl: string;
}
```

Создать `src/AFK4.PlatformControl.Web/src/api/platformClients/supportAccess.ts`:

```ts
import type { PlatformTransport } from '../platformTransport';
import type { SupportAccessGrantIssue } from '../types';

export class SupportAccessApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public issueGrant(
    organizationId: string,
    reason: string,
    lifetimeMinutes: number
  ): Promise<SupportAccessGrantIssue> {
    return this.transport.send<SupportAccessGrantIssue>('POST', '/api/platform/support-access-grants', {
      organizationId,
      reason,
      lifetimeMinutes
    });
  }

  public revokeGrant(grantId: string): Promise<void> {
    return this.transport.send<void>('DELETE', `/api/platform/support-access-grants/${grantId}`);
  }
}
```

Завести поле `public readonly supportAccess: SupportAccessApi;` в `PlatformApiClient` по образцу `supportNotes`.

- [ ] **Step 2: Написать падающий тест секции**

Создать `SupportAccessSection.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { SupportAccessSection } from './SupportAccessSection';

it('выдаёт доступ и открывает админку клиента', async () => {
  const issueGrant = mock().mockResolvedValue({
    grant: { grantId: 'g1', organizationId: 'o1', reason: 'Смена не открывается', issuedAtUtc: '', expiresAtUtc: '', revokedAtUtc: null },
    ticket: 't1',
    adminUrl: 'https://admin.example/support-access?ticket=t1'
  });
  const opened: string[] = [];

  render(
    <I18nProvider><ToastProvider>
      <SupportAccessSection
        client={{ issueGrant, revokeGrant: mock() } as never}
        organizationId="o1"
        openUrl={url => opened.push(url)}
      />
    </ToastProvider></I18nProvider>
  );

  fireEvent.change(screen.getByLabelText('Причина'), {
    target: { value: 'Клуб сообщает, что не открывается смена' }
  });
  fireEvent.click(screen.getByRole('button', { name: 'Войти под клиента' }));

  await waitFor(() => expect(issueGrant).toHaveBeenCalledWith('o1', 'Клуб сообщает, что не открывается смена', 30));
  expect(opened).toEqual(['https://admin.example/support-access?ticket=t1']);
});
```

- [ ] **Step 3: Прогнать — тест падает**

Run: `cd src/AFK4.PlatformControl.Web && bun test SupportAccessSection`
Expected: FAIL — модуля нет.

- [ ] **Step 4: Реализовать секцию**

`SupportAccessSection.tsx` — форма с полем причины (обязательное, минимум 10 символов — столько же требует сервер), выбором срока (15/30 минут) и кнопкой «Войти под клиента». По успеху вызывает `openUrl(issue.adminUrl)`; проп `openUrl` по умолчанию `url => window.open(url, '_blank', 'noopener')` — так тест не трогает реальное окно.

Показывать предупреждение: доступ временный, действия записываются в журнал клуба.

- [ ] **Step 5: Подключить к экрану организации**

В `OrganizationPage.tsx` внутри вкладки `access`, рядом с `OrganizationSupportNotesSection`, отрендерить `SupportAccessSection` при `access.canViewSupport`.

- [ ] **Step 6: Добавить ключи i18n**

Добавить в `locales/ru.json`, `locales/en.json`, `locales/tg.json` ключи `platform.supportAccess.*`: заголовок, подпись поля причины, варианты срока, кнопка, предупреждение, тексты ошибок. Таджикский — настоящий перевод.

Затем: `cd packages/i18n && bun run gen`

- [ ] **Step 7: Прогнать тесты и сборку**

```bash
cd src/AFK4.PlatformControl.Web && bun test && bun run build
cd ../../packages/i18n && bun test
```
Expected: всё зелёное.

- [ ] **Step 8: Коммит**

```bash
git add src locales packages
git commit -m "feat(platform-control): выдача временного доступа под клиента"
```

---

### Task 9: Админка клиента — приём билета и хранение сессии

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/support/supportSession.ts`
- Create: `src/AFK4.OrganizationAdmin.Web/src/support/supportSession.test.ts`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/main.tsx:9-19`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/platformApi.ts:148-180`

**Interfaces:**
- Produces:
  - `interface SupportSession { sessionToken: string; organizationId: string; organizationName: string; reason: string; expiresAtUtc: string; writableAreas: string[] }`
  - `readSupportSession(): SupportSession | null`, `writeSupportSession(session)`, `clearSupportSession()`
  - `redeemSupportTicket(baseUrl: string, ticket: string): Promise<SupportSession>`

- [ ] **Step 1: Написать падающий тест**

Создать `src/AFK4.OrganizationAdmin.Web/src/support/supportSession.test.ts`:

```ts
import { it, expect, beforeEach } from 'bun:test';
import { readSupportSession, writeSupportSession, clearSupportSession } from './supportSession';

beforeEach(() => sessionStorage.clear());

it('хранит и очищает сессию поддержки', () => {
  expect(readSupportSession()).toBeNull();

  writeSupportSession({
    sessionToken: 's1',
    organizationId: 'o1',
    organizationName: 'Клуб',
    reason: 'Смена не открывается',
    expiresAtUtc: '2026-08-06T12:00:00Z',
    writableAreas: ['branch-settings']
  });

  expect(readSupportSession()?.organizationName).toBe('Клуб');

  clearSupportSession();
  expect(readSupportSession()).toBeNull();
});

it('игнорирует испорченное содержимое вместо падения', () => {
  sessionStorage.setItem('afk4.support.session', '{не json');
  expect(readSupportSession()).toBeNull();
});
```

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.OrganizationAdmin.Web && bun test supportSession`
Expected: FAIL — модуля нет.

- [ ] **Step 3: Реализовать хранение**

`supportSession.ts` — по образцу `src/auth/staffSessionStore.ts:4-24`: ключ `afk4.support.session`, `sessionStorage`, разбор в `try/catch` с возвратом `null`. Плюс `redeemSupportTicket`, делающий `POST /api/public/support-access/sessions` и возвращающий разобранный ответ.

- [ ] **Step 4: Принять билет в точке входа**

В `src/AFK4.OrganizationAdmin.Web/src/main.tsx` до `render(...)`: если `window.location.pathname === '/support-access'` и в query есть `ticket` — обменять его, записать сессию, затем `window.history.replaceState(null, '', '/')`, чтобы билет не остался в адресной строке и в истории. При провале обмена — показать понятный экран «ссылка устарела, попросите новую», а не пустую страницу.

- [ ] **Step 5: Научить транспорт слать заголовок сессии**

В `src/AFK4.OrganizationAdmin.Web/src/platformApi.ts` в `fetchAuthorized`/`fetchAuthorizedRaw`: если есть сессия поддержки — вместо `Authorization: Bearer` ставить `X-AFK4-Support-Access-Grant: <sessionToken>`. Отсутствие staff-токена в этом режиме перестаёт быть ошибкой.

- [ ] **Step 6: Прогнать тесты и сборку**

```bash
cd src/AFK4.OrganizationAdmin.Web && bun run test && bun run build
```
Expected: зелёное.

- [ ] **Step 7: Коммит**

```bash
git add src
git commit -m "feat(organization-admin): приём билета поддержки и сессия в транспорте"
```

---

### Task 10: Плашка режима поддержки и гашение недоступного

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/support/SupportModeBanner.tsx`
- Create: `src/AFK4.OrganizationAdmin.Web/src/support/SupportModeBanner.test.tsx`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/App.tsx:52-60`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/App.tsx:300` (построение вкладок)
- Modify: `locales/{ru,en,tg}.json`

- [ ] **Step 1: Написать падающий тест плашки**

```tsx
import { render, screen } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { SupportModeBanner } from './SupportModeBanner';

it('показывает клуб, причину и остаток времени', () => {
  render(
    <I18nProvider>
      <SupportModeBanner
        session={{
          sessionToken: 's1', organizationId: 'o1', organizationName: 'Кибер Арена',
          reason: 'Смена не открывается', expiresAtUtc: new Date(Date.now() + 5 * 60_000).toISOString(),
          writableAreas: ['branch-settings']
        }}
        onExit={mock()}
      />
    </I18nProvider>
  );

  expect(screen.getByText(/Кибер Арена/)).toBeDefined();
  expect(screen.getByText(/Смена не открывается/)).toBeDefined();
  expect(screen.getByRole('button', { name: 'Выйти из режима' })).toBeDefined();
});
```

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.OrganizationAdmin.Web && bun test SupportModeBanner`
Expected: FAIL

- [ ] **Step 3: Реализовать плашку**

Несъёмная полоса поверх приложения: клуб, причина, обратный отсчёт до конца гранта, кнопка «Выйти из режима». Отсчёт пересчитывать раз в секунду; при достижении нуля — вызвать `onExit`. Допуск на рассинхрон часов и защита от `NaN` — как в `useChallengeExpiry.ts` панели платформы (та же задача, тот же подход).

- [ ] **Step 4: Подключить в App**

В `App()` обернуть `<AppInner/>`: если есть сессия поддержки — рендерить плашку над шеллом. `onExit` вызывает `DELETE /api/support-access/session`, чистит хранилище и уводит на страницу входа.

- [ ] **Step 5: Погасить недоступные разделы**

При активной сессии поддержки строить рейл только из воркспейсов, которые режим поддержки реально обслуживает. Денежные разделы (Касса, Смены, Брони) не показывать вовсе — вместо кнопок, которые вернут 403.

Соответствие «область записи → воркспейс» держать в одном месте рядом с `supportSession.ts`, а не размазывать условиями по экранам.

- [ ] **Step 6: Добавить ключи i18n**

`op.support.*`: заголовок плашки, «Выйти из режима», формат остатка времени, объяснение про журнал. Три локали, затем `cd packages/i18n && bun run gen`.

- [ ] **Step 7: Прогнать всё**

```bash
cd src/AFK4.OrganizationAdmin.Web && bun run test && bun run build
cd ../../packages/i18n && bun test
```

- [ ] **Step 8: Коммит**

```bash
git add src locales packages
git commit -m "feat(organization-admin): плашка режима поддержки и скрытие недоступных разделов"
```

---

### Task 11: Сквозная проверка и документация

**Files:**
- Modify: `docs/runbooks/` — добавить рантбук по режиму поддержки
- Test: полный прогон

- [ ] **Step 1: Прогнать весь бэкенд с настоящим Postgres**

```bash
docker run -d --rm --name afk4-pgtest -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=afk4_ci_test -p 55432:5432 postgres:16
export CS='Host=127.0.0.1;Port=55432;Database=afk4_ci_test;Username=postgres;Password=postgres'
export AFK4_POS_POSTGRES_TEST_CONNECTION_STRING="$CS" AFK4_RESERVATION_POSTGRES_TEST_CONNECTION_STRING="$CS" \
       AFK4_COMMERCE_TEST_POSTGRES="$CS" AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING="$CS" \
       AFK4_REQUIRE_POSTGRES_TESTS=1
dotnet test tests/AFK4.Platform.Api.Tests
```
Expected: `Skipped: 0`, 0 failed.

- [ ] **Step 2: Прогнать фронты**

```bash
cd src/AFK4.PlatformControl.Web && bun run test && bun run build
cd ../AFK4.OrganizationAdmin.Web && bun run test && bun run build
cd ../../packages/i18n && bun test
```

- [ ] **Step 3: Написать рантбук**

`docs/runbooks/support-mode.md`: как выдать доступ, что видно клубу в журнале, как отозвать досрочно, что делать, если поддержка сообщает «не пускает» (проверить срок гранта, отзыв, метку эндпоинта).

- [ ] **Step 4: Заполнить конфигурацию среды**

Отметить в рантбуке: `SupportAccess:OrganizationAdminBaseUrl` должен быть задан в среде, иначе ссылка входа соберётся относительной и не откроется. Значение для стейджинга уточнить у владельца.

- [ ] **Step 5: Коммит**

```bash
git add docs
git commit -m "docs: рантбук режима поддержки"
```

---

## Самопроверка плана

**Покрытие спеки (разделы 3 и 4):**
- «GET-эндпоинты помечаются» → Task 5. «Запись по белому списку» → Task 6. «Страж границы» → Task 5, шаг 1 (два теста: области записи и денежный запрет).
- «Билет одноразовый, 60 секунд» → Task 2 (`TicketLifetime`), проверка — Task 2 шаг 1 и Task 7 шаг 1.
- «Сессия живёт до конца гранта, отзыв убивает немедленно» → Task 2 (`AuthenticateSessionAsync` фильтрует `RevokedAtUtc`), проверка — Task 2 шаг 1.
- «Аудит с причиной» → Task 4; причина уже лежит в гранте и попадает в `PlatformSupportContext`.
- «Заголовок в транспорте, плашка, гашение действий, выход отзывает грант» → Tasks 9–10.
- **Не покрыто намеренно:** фильтр «действия поддержки» в журнале панели платформы (раздел 3 спеки, последний абзац). Аудит уже пишет `ActorPlatformAdminUserId`, так что данные для фильтра есть; сам фильтр — отдельная мелкая задача, не блокирующая режим поддержки. Вынести в бэклог.

**Согласованность имён:** `PlatformSupportAccessGrantIssue`, `PlatformSupportSessionDto`, `PlatformSupportWritableAreas`, `IPlatformSupportContextAccessor`, `SupportSession` (фронт) — используются одинаково во всех задачах.

**Риски:**
- Task 5 трогает много файлов механически; ошибка в выборе права даст 403 на чтении. Права брать из существующего вызова авторизации в том же эндпоинте.
- Task 7 удаляет ручные проверки в двух эндпоинтах — если middleware не покрывает их случай, тесты диагностики и журнала упадут; это и есть сигнал.
