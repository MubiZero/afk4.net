# Operator «Клиенты» S3 — правка профиля + деактивация (Implementation Plan)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать оператору править профиль клиента (имя/телефон) и деактивировать/реактивировать его (soft-delete через toggle `IsActive`), собрав действия в меню «⋯» в шапке карточки.

**Architecture:** Два новых операторских эндпоинта на бэке (`PATCH /api/branches/{branchId}/players/{id}` и `POST .../active-state`) через существующий `IBillingCommandService` (переиспускает `ToDto`, аудит как у create). Поле `IsActive` уже есть в БД/сущности/обоих DTO — миграция НЕ нужна. Поиск клиентов параметризуется `includeInactive`, чтобы деактивированный клиент оставался находимым (иначе деактивация была бы необратимой через UI). Фронт: меню «⋯» (kebab) заменяет одиночную PIN-кнопку S2; модалка правки и confirm-деактивации на готовом `PanelModal`; денежные действия блокируются для неактивного клиента.

**Tech Stack:** .NET 10 / ASP.NET minimal API / EF Core (Platform.Api); React 18 + TypeScript + `@afk4/i18n` + lucide-react + `bun test` (happy-dom + @testing-library/react + jest-dom) + Vite (Operator.App.Web).

## Global Constraints

- **Право для правки/деактивации/PIN = `players.create`** (`StaffPermissionNames.CreatePlayerAccount`). НЕ заводить новое право — зеркалит create и существующий set-pin. На фронте — переиспользовать `permissionNames.createPlayerAccount`, новых ключей в `permissionNames` НЕ добавлять.
- **Деактивация — ТОЛЬКО soft** (toggle `IsActive`). Никакого hard-delete: ledger, история, кошелёк клиента обязаны жить.
- **Org-scoping / IDOR:** сущность грузится по `OrganizationId == StaffContext.OrganizationId && HomeBranchId == branchId`; `request.OrganizationId` обязан совпадать с `StaffContext.OrganizationId` (как в create-эндпоинте, строка 114). Чужой/несуществующий игрок → `404` (неразличимы).
- **Аудит обязателен** на каждой операторской мутации: `WriteAuditAsync(..., targetType: "PlayerAccount", targetId: <id>, ...)`. Denied-ветку логировать как create.
- **Деньги** — minor units на проводе; `formatMinorUnits(minor, currency)` ждёт minor. (S3 деньги не двигает, но карточка их показывает.)
- **Таджикский — реальный перевод**, не копия ru. Guard-тест `messages.test.ts` валит `tg===ru` (кроме whitelist заимствований). Voice-guard `voice.test.ts`: без кириллических ALL-CAPS из 4+ букв, без слова «компьютер».
- **Модалки — на `PanelModal`** (паттерн S2), НЕ drawer. Презентационные: реальный вызов держит оркестратор, единый `feedback`.
- **Фронт-тесты:** `bun test` (happy-dom). `App.test` гоняется ОТДЕЛЬНЫМ прогоном (утечка `mock.module` process-wide). Сборка `bun run build` = `tsc + vite` (тесты НЕ тайпчекают — типы ловит только build).
- **Никаких AI-подписей** в коммитах/коде/комментариях. Никаких секретов в коде.

**Команды гейтов:**
- Бэкенд: `dotnet build AFK4.sln` ; тесты — `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BillingEndpointTests"` (узкий фильтр; полный прогон долгий).
- i18n: `cd packages/i18n && bun run gen && bun test`
- Фронт (subdir-тесты): `cd src/AFK4.Operator.App.Web && bun run test`
- Фронт (App.test, отдельно): `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`
- Фронт сборка: `cd src/AFK4.Operator.App.Web && bun run build`

---

## File Structure

**Бэкенд (Platform.Api + Contracts):**
- Create: `src/AFK4.Shared.Contracts/Players/UpdatePlayerAccountRequest.cs` — DTO правки.
- Create: `src/AFK4.Shared.Contracts/Players/SetPlayerActiveStateRequest.cs` — DTO toggle.
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs` — 3 константы аудита.
- Modify: `src/AFK4.Platform.Api/Billing/IBillingCommandService.cs` — 2 сигнатуры.
- Modify: `src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs` — 2 реализации (переиспользуют `ToDto`).
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs` — 2 эндпоинта + `includeInactive` в search.
- Modify: `src/AFK4.Platform.Api/Billing/IOperatorReferenceDataService.cs` + `EfOperatorReferenceDataService.cs` — `includeInactive` параметр.
- Modify (Test): `tests/AFK4.Platform.Api.Tests/BillingEndpointTests.cs` — интеграционные тесты.

**Фронт (Operator.App.Web):**
- Modify: `locales/{ru,en,tg}.json` + регенерация `packages/i18n/src/messages.ts` — новые ключи.
- Modify: `src/api/clients/players.ts` — 2 метода + 2 request-интерфейса + `includeInactive` в `searchPlayers`.
- Create: `src/players/EditProfileModal.tsx` (+ `.test.tsx`) — форма правки.
- Create: `src/players/ActiveStateConfirmModal.tsx` (+ `.test.tsx`) — confirm деактивации/реактивации.
- Create: `src/players/ClientActionsMenu.tsx` (+ `.test.tsx`) — меню «⋯».
- Modify: `src/PanelModal.tsx` — `tone` расширяется до `'warning' | 'danger'`.
- Modify: `src/players/ClientDetail.tsx` (+ `.test.tsx`) — меню вместо PIN-кнопки, баннер «деактивирован», проброс.
- Modify: `src/players/ClientList.tsx` — класс `is-inactive` на строке.
- Modify: `src/BackendPlayersWorkspace.tsx` — стейт/флаги/ветки/монтирование/`includeInactive`.
- Modify: `src/devMockBackend.ts` — мутируемые players + write-хендлеры + `includeInactive` + неактивная фикстура.
- Modify: `src/styles/12-players.css` — baseline CSS меню/модалок/баннера/inactive-строки.

---

## Task 1: Бэкенд-контракты, аудит-имена и командный сервис

**Files:**
- Create: `src/AFK4.Shared.Contracts/Players/UpdatePlayerAccountRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Players/SetPlayerActiveStateRequest.cs`
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs:37-39` (рядом с `CreatePlayerAccount`)
- Modify: `src/AFK4.Platform.Api/Billing/IBillingCommandService.cs:7-11` (после `CreatePlayerAccountAsync`)
- Modify: `src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs` (после `CreatePlayerAccountAsync`, ~строка 89; и рядом с приватным `ToDto`)

**Interfaces:**
- Produces:
  - `UpdatePlayerAccountRequest(Guid OrganizationId, string DisplayName, string? PhoneNumber)` — namespace `AFK4.Shared.Contracts.Players`.
  - `SetPlayerActiveStateRequest(Guid OrganizationId, bool IsActive)` — namespace `AFK4.Shared.Contracts.Players`.
  - `AuditActionNames.UpdatePlayerAccount = "players.update"`, `.DeactivatePlayerAccount = "players.deactivate"`, `.ActivatePlayerAccount = "players.activate"`.
  - `IBillingCommandService.UpdatePlayerAccountAsync(Guid branchId, Guid actorStaffUserId, Guid playerAccountId, UpdatePlayerAccountRequest request, CancellationToken) : Task<BillingCommandServiceResult<PlayerAccountDto>>`.
  - `IBillingCommandService.SetPlayerActiveStateAsync(Guid branchId, Guid actorStaffUserId, Guid playerAccountId, SetPlayerActiveStateRequest request, CancellationToken) : Task<BillingCommandServiceResult<PlayerAccountDto>>`.
- Consumes: `BillingCommandServiceResult<T>` (фабрики `.Ok` / `.Invalid` / `.Missing`); `PlayerAccountDto` (поле `IsActive` уже есть); `EfBillingCommandService.ToDto(PlayerAccountEntity)` (приватный статик, уже маппит `IsActive`).

- [ ] **Step 1: Создать `UpdatePlayerAccountRequest.cs`**

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record UpdatePlayerAccountRequest(
    Guid OrganizationId,
    string DisplayName,
    string? PhoneNumber);
```

- [ ] **Step 2: Создать `SetPlayerActiveStateRequest.cs`**

```csharp
namespace AFK4.Shared.Contracts.Players;

public sealed record SetPlayerActiveStateRequest(
    Guid OrganizationId,
    bool IsActive);
```

- [ ] **Step 3: Добавить аудит-константы** в `AuditActionNames.cs` — сразу после `ViewPlayers` (строка 39):

```csharp
    public const string ViewPlayers = "players.view";

    public const string UpdatePlayerAccount = "players.update";

    public const string DeactivatePlayerAccount = "players.deactivate";

    public const string ActivatePlayerAccount = "players.activate";
```

- [ ] **Step 4: Добавить 2 сигнатуры в `IBillingCommandService.cs`** — после `CreatePlayerAccountAsync` (после строки 11):

```csharp
    Task<BillingCommandServiceResult<PlayerAccountDto>> UpdatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        UpdatePlayerAccountRequest request,
        CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PlayerAccountDto>> SetPlayerActiveStateAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        SetPlayerActiveStateRequest request,
        CancellationToken cancellationToken);
```

Добавить `using AFK4.Shared.Contracts.Players;` в начало файла (рядом с `using AFK4.Shared.Contracts.Billing;`).

- [ ] **Step 5: Реализовать оба метода в `EfBillingCommandService.cs`** — вставить после `CreatePlayerAccountAsync` (после строки 89). Добавить `using AFK4.Shared.Contracts.Players;` к шапке файла.

```csharp
    public async Task<BillingCommandServiceResult<PlayerAccountDto>> UpdatePlayerAccountAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        UpdatePlayerAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            return BillingCommandServiceResult<PlayerAccountDto>.Invalid("Display name is required.");
        }

        var player = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
            p => p.PlayerAccountId == playerAccountId
                && p.OrganizationId == request.OrganizationId
                && p.HomeBranchId == branchId,
            cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<PlayerAccountDto>.Missing("Player not found.");
        }

        player.DisplayName = request.DisplayName.Trim();
        player.PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim();
        await dbContext.SaveChangesAsync(cancellationToken);

        return BillingCommandServiceResult<PlayerAccountDto>.Ok(ToDto(player));
    }

    public async Task<BillingCommandServiceResult<PlayerAccountDto>> SetPlayerActiveStateAsync(
        Guid branchId,
        Guid actorStaffUserId,
        Guid playerAccountId,
        SetPlayerActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.PlayerAccounts.SingleOrDefaultAsync(
            p => p.PlayerAccountId == playerAccountId
                && p.OrganizationId == request.OrganizationId
                && p.HomeBranchId == branchId,
            cancellationToken);
        if (player is null)
        {
            return BillingCommandServiceResult<PlayerAccountDto>.Missing("Player not found.");
        }

        player.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        return BillingCommandServiceResult<PlayerAccountDto>.Ok(ToDto(player));
    }
```

> Примечание: грузим tracked (без `AsNoTracking`) — нужно мутировать и сохранить. `ToDto` — существующий приватный статик-метод этого класса (строки ~654-664), уже маппит `IsActive`. `actorStaffUserId` в сигнатуре для симметрии с create; аудит пишет эндпоинт (Task 2), не сервис.

- [ ] **Step 6: Собрать бэкенд**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Shared.Contracts/Players/UpdatePlayerAccountRequest.cs \
        src/AFK4.Shared.Contracts/Players/SetPlayerActiveStateRequest.cs \
        src/AFK4.Platform.Api/Audit/AuditActionNames.cs \
        src/AFK4.Platform.Api/Billing/IBillingCommandService.cs \
        src/AFK4.Platform.Api/Billing/EfBillingCommandService.cs
git commit -m "feat(players-s3): контракты правки/деактивации + командный сервис (UpdatePlayerAccount/SetPlayerActiveState)"
```

---

## Task 2: Эндпоинты PATCH-правки и POST-active-state + интеграционные тесты

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs` (после set-pin эндпоинта, ~строка 185)
- Test: `tests/AFK4.Platform.Api.Tests/BillingEndpointTests.cs` (новые `[Fact]` + при необходимости хелпер сидирования неактивного)

**Interfaces:**
- Consumes: `IBillingCommandService.UpdatePlayerAccountAsync` / `.SetPlayerActiveStateAsync` (Task 1); `StaffAuthorizationService.RequireBranchPermissionAsync`; `WriteAuditAsync`; `ToHttpResult`; `AuditActionNames.{UpdatePlayerAccount,DeactivatePlayerAccount,ActivatePlayerAccount}`.
- Produces:
  - `PATCH /api/branches/{branchId:guid}/players/{playerAccountId:guid}` ← `UpdatePlayerAccountRequest` → `200 PlayerAccountDto`.
  - `POST /api/branches/{branchId:guid}/players/{playerAccountId:guid}/active-state` ← `SetPlayerActiveStateRequest` → `200 PlayerAccountDto`.

- [ ] **Step 1: Написать падающие тесты** в `BillingEndpointTests.cs` (вставить новые `[Fact]` после теста создания, ~строка 60). `SeedPlayerAsync` (строка 571) уже сидит активного игрока `PlayerAccountId` в `TestIds.BranchId`. `CashierOperator` имеет право `players.create`.

```csharp
    [Fact]
    public async Task UpdatePlayer_WithCashier_UpdatesNameAndPhoneAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "Player Renamed", "+992000000099"));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.Equal("Player Renamed", body.DisplayName);
        Assert.Equal("+992000000099", body.PhoneNumber);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == PlayerAccountId);
        Assert.Equal("Player Renamed", stored.DisplayName);
        var audit = await dbContext.AuditRecords.SingleAsync(a => a.Action == AuditActionNames.UpdatePlayerAccount);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
        Assert.Equal(PlayerAccountId.ToString("D"), audit.TargetId);
    }

    [Fact]
    public async Task UpdatePlayer_BlankName_ReturnsBadRequest()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdatePlayer_UnknownPlayer_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.PatchAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{Guid.NewGuid():D}",
            new UpdatePlayerAccountRequest(TestIds.OrganizationId, "Ghost", null));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeactivatePlayer_WithCashier_SetsInactiveAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.False(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var stored = await dbContext.PlayerAccounts.SingleAsync(p => p.PlayerAccountId == PlayerAccountId);
        Assert.False(stored.IsActive);
        var audit = await dbContext.AuditRecords.SingleAsync(a => a.Action == AuditActionNames.DeactivatePlayerAccount);
        Assert.Equal(AuditOutcome.Succeeded, audit.Outcome);
    }

    [Fact]
    public async Task ReactivatePlayer_WithCashier_SetsActiveAndAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        // деактивируем, затем реактивируем
        await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));

        var response = await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, true));
        var body = await response.Content.ReadFromJsonAsync<PlayerAccountDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(body);
        Assert.True(body.IsActive);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await dbContext.AuditRecords.AnyAsync(a => a.Action == AuditActionNames.ActivatePlayerAccount));
    }
```

Добавить `using AFK4.Shared.Contracts.Players;` к шапке теста, если ещё нет.

- [ ] **Step 2: Прогнать тесты — убедиться, что падают**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BillingEndpointTests"`
Expected: компиляция падает (`PatchAsJsonAsync` нет таких эндпоинтов → 404/тесты красные). Это RED.

- [ ] **Step 3: Добавить эндпоинты** в `PlayerManagementEndpoints.cs` — вставить сразу после set-pin эндпоинта (после строки 185, перед search-эндпоинтом). Зеркалит create-эндпоинт (auth → org-match → service → audit → ok).

```csharp
        app.MapPatch("/api/branches/{branchId:guid}/players/{playerAccountId:guid}", async (
            Guid branchId,
            Guid playerAccountId,
            UpdatePlayerAccountRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    AuditActionNames.UpdatePlayerAccount,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.DisplayName, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await billingCommandService.UpdatePlayerAccountAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                playerAccountId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.UpdatePlayerAccount,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.DisplayName },
                cancellationToken);

            return Results.Ok(result.Response);
        });

        app.MapPost("/api/branches/{branchId:guid}/players/{playerAccountId:guid}/active-state", async (
            Guid branchId,
            Guid playerAccountId,
            SetPlayerActiveStateRequest request,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IBillingCommandService billingCommandService,
            CancellationToken cancellationToken) =>
        {
            var auditAction = request.IsActive
                ? AuditActionNames.ActivatePlayerAccount
                : AuditActionNames.DeactivatePlayerAccount;

            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId,
                StaffPermissionNames.CreatePlayerAccount,
                cancellationToken);

            if (!authorization.IsAuthenticated)
            {
                return Results.Unauthorized();
            }

            if (!authorization.IsAllowed)
            {
                await WriteAuditAsync(
                    auditRecordWriter,
                    authorization.StaffContext!.OrganizationId,
                    branchId,
                    authorization.StaffContext.StaffUserId,
                    auditAction,
                    "PlayerAccount",
                    playerAccountId.ToString("D"),
                    AuditOutcome.Denied,
                    new { request.IsActive, authorization.DenialReason },
                    cancellationToken);

                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            if (request.OrganizationId != authorization.StaffContext!.OrganizationId)
            {
                return Results.BadRequest(new { Error = "OrganizationId must match the authenticated staff organization." });
            }

            var result = await billingCommandService.SetPlayerActiveStateAsync(
                branchId,
                authorization.StaffContext.StaffUserId,
                playerAccountId,
                request,
                cancellationToken);

            if (!result.Succeeded)
            {
                return ToHttpResult(result);
            }

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                branchId,
                authorization.StaffContext.StaffUserId,
                auditAction,
                "PlayerAccount",
                playerAccountId.ToString("D"),
                AuditOutcome.Succeeded,
                new { request.IsActive },
                cancellationToken);

            return Results.Ok(result.Response);
        });
```

> `AFK4.Shared.Contracts.Players` уже в using-блоке файла (строка 44). `MapPatch` — стандартный метод minimal API.

- [ ] **Step 4: Прогнать тесты — убедиться, что зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BillingEndpointTests"`
Expected: все тесты PASS (включая 5 новых).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs tests/AFK4.Platform.Api.Tests/BillingEndpointTests.cs
git commit -m "feat(players-s3): эндпоинты PATCH-правки и active-state + аудит + интеграционные тесты"
```

---

## Task 3: Поиск клиентов — параметр `includeInactive`

**Files:**
- Modify: `src/AFK4.Platform.Api/Billing/IOperatorReferenceDataService.cs:7-12`
- Modify: `src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs:16-46`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs:187-231` (search-эндпоинт)
- Test: `tests/AFK4.Platform.Api.Tests/BillingEndpointTests.cs`

**Interfaces:**
- Produces: `SearchPlayersAsync(Guid organizationId, Guid branchId, string? query, int limit, bool includeInactive, CancellationToken)`. Эндпоинт `GET /api/branches/{branchId}/players?query=&limit=&includeInactive=` (новый bool-параметр, по умолчанию `false`).
- Consumes: `PlayerSearchResultDto` (поле `isActive` уже есть).

> **Зачем:** сейчас `SearchPlayersAsync` жёстко режет `player.IsActive` (строка 39) — деактивированный клиент исчезает из поиска и его невозможно реактивировать через UI. По умолчанию поведение сохраняем (Касса/Брони не должны видеть неактивных), а воркспейс «Клиенты» зовёт с `includeInactive=true`.

- [ ] **Step 1: Написать падающий тест** в `BillingEndpointTests.cs` (после тестов Task 2). Запрос требует ≥2 символа (`MinimumSearchLength`), поэтому ищем по части имени `"Player"`.

```csharp
    [Fact]
    public async Task SearchPlayers_ExcludesInactiveByDefault_IncludesWhenRequested()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);
        await SeedPlayerAsync(factory);
        // деактивируем сид-игрока
        await client.PostAsJsonAsync(
            $"/api/branches/{TestIds.BranchId:D}/players/{PlayerAccountId:D}/active-state",
            new SetPlayerActiveStateRequest(TestIds.OrganizationId, false));

        var defaultSearch = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(
            $"/api/branches/{TestIds.BranchId:D}/players?query=Player");
        var inclusiveSearch = await client.GetFromJsonAsync<List<PlayerSearchResultDto>>(
            $"/api/branches/{TestIds.BranchId:D}/players?query=Player&includeInactive=true");

        Assert.NotNull(defaultSearch);
        Assert.DoesNotContain(defaultSearch!, p => p.PlayerAccountId == PlayerAccountId);
        Assert.NotNull(inclusiveSearch);
        Assert.Contains(inclusiveSearch!, p => p.PlayerAccountId == PlayerAccountId && !p.IsActive);
    }
```

Добавить `using AFK4.Shared.Contracts.Operator;` к шапке теста, если ещё нет (для `PlayerSearchResultDto`).

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~SearchPlayers_ExcludesInactive"`
Expected: компиляция/тест падает (параметра `includeInactive` нет, неактивный никогда не возвращается).

- [ ] **Step 3: Расширить интерфейс** `IOperatorReferenceDataService.cs` — добавить параметр в `SearchPlayersAsync`:

```csharp
    Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        int limit,
        bool includeInactive,
        CancellationToken cancellationToken);
```

- [ ] **Step 4: Обновить реализацию** `EfOperatorReferenceDataService.cs` — сигнатура (строки 16-21) и Where-фильтр (строки 36-39):

Сигнатура:
```csharp
    public async Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid organizationId,
        Guid branchId,
        string? query,
        int limit,
        bool includeInactive,
        CancellationToken cancellationToken)
```

Фильтр (заменить строки 36-39):
```csharp
            .Where(player =>
                player.OrganizationId == organizationId &&
                player.HomeBranchId == branchId &&
                (includeInactive || player.IsActive))
```

- [ ] **Step 5: Обновить search-эндпоинт** `PlayerManagementEndpoints.cs` — добавить параметр `bool? includeInactive` в делегат (после `int? limit`, строка 190) и прокинуть в сервис (вызов на строках 223-228):

Делегат (добавить параметр):
```csharp
        app.MapGet("/api/branches/{branchId:guid}/players", async (
            Guid branchId,
            string? query,
            int? limit,
            bool? includeInactive,
            StaffAuthorizationService authorizationService,
```

Вызов сервиса:
```csharp
            var players = await referenceDataService.SearchPlayersAsync(
                authorization.StaffContext!.OrganizationId,
                branchId,
                query,
                limit ?? 20,
                includeInactive ?? false,
                cancellationToken);
```

- [ ] **Step 6: Проверить другие вызовы `SearchPlayersAsync`**

Run: `grep -rn "SearchPlayersAsync" src/ tests/`
Expected: единственный продакшн-вызов — в search-эндпоинте (обновлён). Если есть другие — добавить `false` пятым аргументом. (Тестовые вызовы DI идут через HTTP, прямых вызовов сервиса нет.)

- [ ] **Step 7: Прогнать тесты — зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BillingEndpointTests"`
Expected: все PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api/Billing/IOperatorReferenceDataService.cs \
        src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs \
        src/AFK4.Platform.Api/Endpoints/PlayerManagementEndpoints.cs \
        tests/AFK4.Platform.Api.Tests/BillingEndpointTests.cs
git commit -m "feat(players-s3): поиск клиентов — параметр includeInactive (неактивные находимы для реактивации)"
```

---

## Task 4: i18n-ключи (ru/en/tg) для меню, правки, деактивации

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (плоские ключи с точками; секция `op.players.*`)
- Regenerate: `packages/i18n/src/messages.ts` (через `bun run gen`)

**Interfaces:**
- Produces ключи (все три локали):
  - `op.players.menu.open` — aria-label кнопки «⋯».
  - `op.players.actions.editProfileLabel`, `op.players.actions.deactivateLabel`, `op.players.actions.reactivateLabel`.
  - `op.players.editProfile.title`, `.subtitle`, `.nameLabel`, `.phoneLabel`, `.submit`.
  - `op.players.deactivate.title`, `.subtitle`, `.impact`, `.confirm`.
  - `op.players.reactivate.title`, `.subtitle`, `.impact`, `.confirm`.
  - `op.players.detail.deactivatedBanner` — плашка в карточке неактивного.
  - `op.players.error.noPermEditProfile`, `op.players.error.noPermActiveState`, `op.players.error.editNameRequired`.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`** (в секцию `op.players.*`, рядом с существующими `op.players.pin.*` / `op.players.actions.*`):

```json
  "op.players.menu.open": "Действия с клиентом",
  "op.players.actions.editProfileLabel": "Править профиль",
  "op.players.actions.deactivateLabel": "Деактивировать",
  "op.players.actions.reactivateLabel": "Активировать",
  "op.players.editProfile.title": "Правка профиля",
  "op.players.editProfile.subtitle": "Имя и телефон клиента",
  "op.players.editProfile.nameLabel": "Имя",
  "op.players.editProfile.phoneLabel": "Телефон",
  "op.players.editProfile.submit": "Сохранить",
  "op.players.deactivate.title": "Деактивировать клиента?",
  "op.players.deactivate.subtitle": "Клиент станет неактивным",
  "op.players.deactivate.impact": "История и кошелёк сохранятся. Денежные операции и вход на место станут недоступны, пока клиент не активирован снова.",
  "op.players.deactivate.confirm": "Деактивировать",
  "op.players.reactivate.title": "Активировать клиента?",
  "op.players.reactivate.subtitle": "Клиент снова станет активным",
  "op.players.reactivate.impact": "Клиент вернётся в общий список, денежные операции снова станут доступны.",
  "op.players.reactivate.confirm": "Активировать",
  "op.players.detail.deactivatedBanner": "Клиент деактивирован. Операции с деньгами заблокированы.",
  "op.players.error.noPermEditProfile": "Нет права на правку профиля клиента.",
  "op.players.error.noPermActiveState": "Нет права менять активность клиента.",
  "op.players.error.editNameRequired": "Укажите имя клиента."
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`** (английский):

```json
  "op.players.menu.open": "Client actions",
  "op.players.actions.editProfileLabel": "Edit profile",
  "op.players.actions.deactivateLabel": "Deactivate",
  "op.players.actions.reactivateLabel": "Activate",
  "op.players.editProfile.title": "Edit profile",
  "op.players.editProfile.subtitle": "Client name and phone",
  "op.players.editProfile.nameLabel": "Name",
  "op.players.editProfile.phoneLabel": "Phone",
  "op.players.editProfile.submit": "Save",
  "op.players.deactivate.title": "Deactivate client?",
  "op.players.deactivate.subtitle": "The client will become inactive",
  "op.players.deactivate.impact": "History and wallet are kept. Money operations and seat sign-in stay unavailable until the client is activated again.",
  "op.players.deactivate.confirm": "Deactivate",
  "op.players.reactivate.title": "Activate client?",
  "op.players.reactivate.subtitle": "The client will become active again",
  "op.players.reactivate.impact": "The client returns to the main list and money operations become available again.",
  "op.players.reactivate.confirm": "Activate",
  "op.players.detail.deactivatedBanner": "Client is deactivated. Money operations are blocked.",
  "op.players.error.noPermEditProfile": "No permission to edit the client profile.",
  "op.players.error.noPermActiveState": "No permission to change the client's active state.",
  "op.players.error.editNameRequired": "Enter the client's name."
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json`** — РЕАЛЬНЫЙ таджикский (НЕ копия ru). Таджикский — кириллический, с буквами ӣ/ӯ/ҳ/қ/ғ/ҷ:

```json
  "op.players.menu.open": "Амалҳо бо мизоҷ",
  "op.players.actions.editProfileLabel": "Таҳрири профил",
  "op.players.actions.deactivateLabel": "Ғайрифаъол кардан",
  "op.players.actions.reactivateLabel": "Фаъол кардан",
  "op.players.editProfile.title": "Таҳрири профил",
  "op.players.editProfile.subtitle": "Ном ва телефони мизоҷ",
  "op.players.editProfile.nameLabel": "Ном",
  "op.players.editProfile.phoneLabel": "Телефон",
  "op.players.editProfile.submit": "Нигоҳ доштан",
  "op.players.deactivate.title": "Мизоҷ ғайрифаъол карда шавад?",
  "op.players.deactivate.subtitle": "Мизоҷ ғайрифаъол мешавад",
  "op.players.deactivate.impact": "Таърих ва ҳамён нигоҳ дошта мешаванд. Амалиёти пулӣ ва вуруд ба ҷой то фаъол кардани дубораи мизоҷ дастнорас мемонанд.",
  "op.players.deactivate.confirm": "Ғайрифаъол кардан",
  "op.players.reactivate.title": "Мизоҷ фаъол карда шавад?",
  "op.players.reactivate.subtitle": "Мизоҷ дубора фаъол мешавад",
  "op.players.reactivate.impact": "Мизоҷ ба рӯйхати умумӣ бармегардад ва амалиёти пулӣ дубора дастрас мешавад.",
  "op.players.reactivate.confirm": "Фаъол кардан",
  "op.players.detail.deactivatedBanner": "Мизоҷ ғайрифаъол аст. Амалиёти пулӣ баста шудааст.",
  "op.players.error.noPermEditProfile": "Барои таҳрири профили мизоҷ ҳуқуқ нест.",
  "op.players.error.noPermActiveState": "Барои тағйири фаъолнокии мизоҷ ҳуқуқ нест.",
  "op.players.error.editNameRequired": "Номи мизоҷро ворид кунед."
```

- [ ] **Step 4: Регенерировать и прогнать guard**

Run: `cd packages/i18n && bun run gen && bun test`
Expected: PASS — parity (ru/en/tg одинаковый набор ключей) + tg≠ru guard зелёные. Если guard ругается на конкретный ключ как `tg===ru` — это значит таджикский совпал с русским буквально (для этих фраз не должен); поправить перевод.

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(players-s3): i18n-ключи меню/правки/деактивации (ru/en/tg, таджикский реальный)"
```

---

## Task 5: API-клиент — методы правки/активности + `includeInactive` в поиске

**Files:**
- Modify: `src/api/clients/players.ts` (request-интерфейсы + методы; сигнатура `searchPlayers`)

**Interfaces:**
- Produces (на объекте `createPlayerClient(api)`):
  - `searchPlayers(branchId, query, limit, includeInactive?: boolean): Promise<PlayerSearchResultDto[]>`.
  - `updateProfile(branchId, playerAccountId, request: UpdatePlayerAccountRequest): Promise<PlayerAccountDto>` → `PATCH /api/branches/{branchId}/players/{playerAccountId}`.
  - `setActiveState(branchId, playerAccountId, request: SetPlayerActiveStateRequest): Promise<PlayerAccountDto>` → `POST /api/branches/{branchId}/players/{playerAccountId}/active-state`.
  - Интерфейсы `UpdatePlayerAccountRequest { organizationId, displayName, phoneNumber }` и `SetPlayerActiveStateRequest { organizationId, isActive }`.
- Consumes: `PlatformApiClient.get/post`; `api.patch` (проверить наличие — см. Step 1).

- [ ] **Step 1: Проверить, что у `PlatformApiClient` есть `patch`**

Run: `grep -n "patch\b\|patch<" src/AFK4.Operator.App.Web/src/platformApi.ts`
Expected: метод `patch<T, B>(path, body)` существует. **Если НЕ существует** — добавить его в `platformApi.ts` по образцу `post` (тот же код, но `method: 'PATCH'`), отдельным под-шагом, и упомянуть в коммите.

- [ ] **Step 2: Добавить request-интерфейсы** в `players.ts` — после `SetPlayerPinRequest` (строка 117):

```ts
export interface UpdatePlayerAccountRequest {
  organizationId: Guid;
  displayName: string;
  phoneNumber: string | null;
}

export interface SetPlayerActiveStateRequest {
  organizationId: Guid;
  isActive: boolean;
}
```

- [ ] **Step 3: Расширить `searchPlayers` параметром `includeInactive`** (строки 121-123):

```ts
    searchPlayers(branchId: Guid, query: string, limit: number, includeInactive = false): Promise<PlayerSearchResultDto[]> {
      const params: Record<string, string | number> = { query, limit };
      if (includeInactive) params.includeInactive = 'true';
      return api.get<PlayerSearchResultDto[]>(`/api/branches/${branchId}/players`, params);
    },
```

- [ ] **Step 4: Добавить два метода** в объект `createPlayerClient` — после `setPlayerPin` (перед закрывающей `}` на строке 161, добавив запятую к `setPlayerPin`):

```ts
    updateProfile(branchId: Guid, playerAccountId: Guid, request: UpdatePlayerAccountRequest): Promise<PlayerAccountDto> {
      return api.patch<PlayerAccountDto, UpdatePlayerAccountRequest>(`/api/branches/${branchId}/players/${playerAccountId}`, request);
    },
    setActiveState(branchId: Guid, playerAccountId: Guid, request: SetPlayerActiveStateRequest): Promise<PlayerAccountDto> {
      return api.post<PlayerAccountDto, SetPlayerActiveStateRequest>(`/api/branches/${branchId}/players/${playerAccountId}/active-state`, request);
    }
```

- [ ] **Step 5: Собрать фронт (тайпчек)**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: build PASS (метод `searchPlayers` обратно совместим — 4-й аргумент опционален; существующие вызовы не ломаются).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/api/clients/players.ts src/AFK4.Operator.App.Web/src/platformApi.ts
git commit -m "feat(players-s3): API-клиент updateProfile/setActiveState + includeInactive в searchPlayers"
```

---

## Task 6: `EditProfileModal` — форма правки имени/телефона

**Files:**
- Create: `src/players/EditProfileModal.tsx`
- Test: `src/players/EditProfileModal.test.tsx`

**Interfaces:**
- Produces: `EditProfileModal({ name, phone, onChangeName, onChangePhone, onClose, onSubmit, busy })`. Submit заблокирован при пустом имени (`name.trim().length === 0`) или `busy`.
- Consumes: `PanelModal`; i18n-ключи `op.players.editProfile.*` (Task 4).

Образец — `NewClientModal.tsx` (форма имя+телефон). Отличие: предзаполненные поля, submit «Сохранить», `busy`-дизейбл (мутация существующего клиента).

- [ ] **Step 1: Написать падающий тест** `EditProfileModal.test.tsx` (стиль `PinModal.test.tsx`):

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { EditProfileModal } from './EditProfileModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof EditProfileModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <EditProfileModal
        name="Madina S."
        phone="+992 90 555 22 11"
        onChangeName={() => {}}
        onChangePhone={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit };
};

describe('EditProfileModal', () => {
  it('prefills name and phone', () => {
    renderModal();
    expect(screen.getByLabelText('Имя')).toHaveValue('Madina S.');
    expect(screen.getByLabelText('Телефон')).toHaveValue('+992 90 555 22 11');
  });

  it('disables submit when name is blank', () => {
    renderModal({ name: '   ' });
    expect(screen.getByRole('button', { name: /Сохранить/ })).toBeDisabled();
  });

  it('fires onSubmit when valid', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Сохранить/ }));
    expect(onSubmit).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/EditProfileModal.test.tsx`
Expected: FAIL (`EditProfileModal` не существует).

- [ ] **Step 3: Создать `EditProfileModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { Save } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Правка профиля клиента (имя/телефон). Презентационный: реальный вызов updateProfile —
// в оркестраторе. Submit заблокирован при пустом имени (имя обязательно, зеркало бэка).
export function EditProfileModal({
  name,
  phone,
  onChangeName,
  onChangePhone,
  onClose,
  onSubmit,
  busy,
}: {
  name: string;
  phone: string;
  onChangeName: (value: string) => void;
  onChangePhone: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const nameEmpty = name.trim().length === 0;

  return (
    <PanelModal
      title={t('op.players.editProfile.title')}
      subtitle={t('op.players.editProfile.subtitle')}
      onClose={onClose}
    >
      <form
        className="clients-edit-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="edit-client-name">{t('op.players.editProfile.nameLabel')}</label>
        <input
          id="edit-client-name"
          value={name}
          autoFocus
          disabled={busy}
          onChange={(event) => onChangeName(event.currentTarget.value)}
        />
        <label htmlFor="edit-client-phone">{t('op.players.editProfile.phoneLabel')}</label>
        <input
          id="edit-client-phone"
          value={phone}
          inputMode="tel"
          disabled={busy}
          onChange={(event) => onChangePhone(event.currentTarget.value)}
        />
        <button type="submit" className="clients-primary-action" disabled={busy || nameEmpty}>
          <Save size={15} aria-hidden="true" />
          {t('op.players.editProfile.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогнать — зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/EditProfileModal.test.tsx`
Expected: 3/3 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/EditProfileModal.tsx src/AFK4.Operator.App.Web/src/players/EditProfileModal.test.tsx
git commit -m "feat(players-s3): EditProfileModal — форма правки имени/телефона"
```

---

## Task 7: `ActiveStateConfirmModal` + danger-tone у `PanelModal`

**Files:**
- Modify: `src/PanelModal.tsx:19` (`tone?: 'warning'` → `tone?: 'warning' | 'danger'`)
- Create: `src/players/ActiveStateConfirmModal.tsx`
- Test: `src/players/ActiveStateConfirmModal.test.tsx`

**Interfaces:**
- Produces: `ActiveStateConfirmModal({ mode, onClose, onConfirm, busy })`, где `mode: 'deactivate' | 'reactivate'`. `deactivate` → `tone="danger"`, заголовок/кнопка деактивации; `reactivate` → `tone="warning"`, заголовок/кнопка активации.
- Consumes: `PanelModal` (расширенный `tone`); i18n `op.players.deactivate.*` / `op.players.reactivate.*`.

> Расширение `PanelModal.tone` убирает «полу-наличие» (#32): был только `warning`, а destructive-подтверждение требует визуально отличного danger-акцента.

- [ ] **Step 1: Расширить `tone` в `PanelModal.tsx`** (строка 19):

```ts
  tone?: 'warning' | 'danger';
```

(Остальной код не меняется — `className={`panel-modal${tone ? ` ${tone}` : ''}`}` уже подставит класс `danger`.)

- [ ] **Step 2: Написать падающий тест** `ActiveStateConfirmModal.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ActiveStateConfirmModal } from './ActiveStateConfirmModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof ActiveStateConfirmModal>[0]> = {}) => {
  const onConfirm = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ActiveStateConfirmModal mode="deactivate" onClose={() => {}} onConfirm={onConfirm} busy={false} {...over} />
    </I18nProvider>
  );
  return { onConfirm };
};

describe('ActiveStateConfirmModal', () => {
  it('shows deactivate copy in deactivate mode', () => {
    renderModal();
    expect(screen.getByText('Деактивировать клиента?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Деактивировать/ })).toBeInTheDocument();
  });

  it('shows reactivate copy in reactivate mode', () => {
    renderModal({ mode: 'reactivate' });
    expect(screen.getByText('Активировать клиента?')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Активировать/ })).toBeInTheDocument();
  });

  it('fires onConfirm on confirm click', () => {
    const { onConfirm } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Деактивировать/ }));
    expect(onConfirm).toHaveBeenCalled();
  });

  it('disables confirm when busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Деактивировать/ })).toBeDisabled();
  });
});
```

- [ ] **Step 3: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ActiveStateConfirmModal.test.tsx`
Expected: FAIL (компонента нет).

- [ ] **Step 4: Создать `ActiveStateConfirmModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { Power, PowerOff } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Подтверждение деактивации/реактивации клиента. Деактивация — destructive (tone=danger);
// реактивация мягче (tone=warning). Реальный toggle IsActive держит оркестратор.
export function ActiveStateConfirmModal({
  mode,
  onClose,
  onConfirm,
  busy,
}: {
  mode: 'deactivate' | 'reactivate';
  onClose: () => void;
  onConfirm: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const isDeactivate = mode === 'deactivate';
  const ns = isDeactivate ? 'op.players.deactivate' : 'op.players.reactivate';

  return (
    <PanelModal
      title={t(`${ns}.title` as Parameters<typeof t>[0])}
      subtitle={t(`${ns}.subtitle` as Parameters<typeof t>[0])}
      onClose={onClose}
      tone={isDeactivate ? 'danger' : 'warning'}
    >
      <div className="clients-confirm">
        <p className="clients-confirm-impact">{t(`${ns}.impact` as Parameters<typeof t>[0])}</p>
        <button
          type="button"
          className={isDeactivate ? 'clients-danger-action' : 'clients-primary-action'}
          disabled={busy}
          onClick={onConfirm}
        >
          {isDeactivate ? <PowerOff size={15} aria-hidden="true" /> : <Power size={15} aria-hidden="true" />}
          {t(`${ns}.confirm` as Parameters<typeof t>[0])}
        </button>
      </div>
    </PanelModal>
  );
}
```

> Если динамические ключи `t(`${ns}.title`)` не проходят тип `MessageKey`, заменить на явный тернарник: `t(isDeactivate ? 'op.players.deactivate.title' : 'op.players.reactivate.title')` для каждого из 4 ключей. Реализатор выбирает то, что тайпчекается под `bun run build`.

- [ ] **Step 5: Прогнать — зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ActiveStateConfirmModal.test.tsx`
Expected: 4/4 PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/PanelModal.tsx \
        src/AFK4.Operator.App.Web/src/players/ActiveStateConfirmModal.tsx \
        src/AFK4.Operator.App.Web/src/players/ActiveStateConfirmModal.test.tsx
git commit -m "feat(players-s3): ActiveStateConfirmModal + danger-tone у PanelModal"
```

---

## Task 8: `ClientActionsMenu` — меню «⋯» (kebab) с a11y

**Files:**
- Create: `src/players/ClientActionsMenu.tsx`
- Test: `src/players/ClientActionsMenu.test.tsx`

**Interfaces:**
- Produces: `ClientActionsMenu({ isActive, onEditProfile, onSetPin, onToggleActive })`. Рендерит кнопку «⋯» (`aria-haspopup="menu"`, `aria-expanded`), при открытии — dropdown `role="menu"` с пунктами: «Править профиль», «PIN», «Деактивировать»/«Активировать» (по `isActive`). Закрывается по Escape, клику вне, и после выбора пункта.
- Consumes: i18n `op.players.menu.open`, `op.players.actions.{editProfileLabel,pinLabel,deactivateLabel,reactivateLabel}` (`pinLabel` уже существует с S2).

> В проекте нет переиспользуемого kebab-примитива (grep по MoreHorizontal/MoreVertical пуст) — собираем локальный, a11y-корректный. Все три действия под одним правом `players.create`, поэтому ClientDetail рендерит меню только при наличии права (Task 9); сам компонент правом не управляет.

- [ ] **Step 1: Написать падающий тест** `ClientActionsMenu.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientActionsMenu } from './ClientActionsMenu';

afterEach(cleanup);

const renderMenu = (over: Partial<Parameters<typeof ClientActionsMenu>[0]> = {}) => {
  const onEditProfile = mock(() => {});
  const onSetPin = mock(() => {});
  const onToggleActive = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <ClientActionsMenu
        isActive={true}
        onEditProfile={onEditProfile}
        onSetPin={onSetPin}
        onToggleActive={onToggleActive}
        {...over}
      />
    </I18nProvider>
  );
  return { onEditProfile, onSetPin, onToggleActive };
};

describe('ClientActionsMenu', () => {
  it('hides the menu until opened', () => {
    renderMenu();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('opens on trigger click and shows items', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('menu')).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Править профиль/ })).toBeInTheDocument();
    expect(screen.getByRole('menuitem', { name: /Деактивировать/ })).toBeInTheDocument();
  });

  it('shows reactivate label when client is inactive', () => {
    renderMenu({ isActive: false });
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    expect(screen.getByRole('menuitem', { name: /Активировать/ })).toBeInTheDocument();
  });

  it('calls handler and closes after selecting an item', () => {
    const { onEditProfile } = renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.click(screen.getByRole('menuitem', { name: /Править профиль/ }));
    expect(onEditProfile).toHaveBeenCalled();
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });

  it('closes on Escape', () => {
    renderMenu();
    fireEvent.click(screen.getByRole('button', { name: 'Действия с клиентом' }));
    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('menu')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientActionsMenu.test.tsx`
Expected: FAIL (компонента нет).

- [ ] **Step 3: Создать `ClientActionsMenu.tsx`**

```tsx
import { useEffect, useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { MoreHorizontal, Pencil, KeyRound, Power, PowerOff } from 'lucide-react';

// Меню «⋯» действий с клиентом в шапке карточки. a11y: кнопка с aria-haspopup/aria-expanded,
// список role=menu/menuitem, закрытие по Escape и клику вне. Гейтинг по праву — выше (ClientDetail).
export function ClientActionsMenu({
  isActive,
  onEditProfile,
  onSetPin,
  onToggleActive,
}: {
  isActive: boolean;
  onEditProfile: () => void;
  onSetPin: () => void;
  onToggleActive: () => void;
}) {
  const { t } = useI18n();
  const [open, setOpen] = useState(false);
  const rootRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) {
      return undefined;
    }
    const onKey = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setOpen(false);
      }
    };
    const onPointer = (event: PointerEvent) => {
      if (rootRef.current && !rootRef.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    document.addEventListener('keydown', onKey);
    document.addEventListener('pointerdown', onPointer);
    return () => {
      document.removeEventListener('keydown', onKey);
      document.removeEventListener('pointerdown', onPointer);
    };
  }, [open]);

  const select = (handler: () => void) => {
    setOpen(false);
    handler();
  };

  return (
    <div className="client-actions-menu" ref={rootRef}>
      <button
        type="button"
        className="client-actions-trigger"
        aria-label={t('op.players.menu.open')}
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((current) => !current)}
      >
        <MoreHorizontal size={16} aria-hidden="true" />
      </button>
      {open && (
        <div className="client-actions-dropdown" role="menu">
          <button type="button" role="menuitem" className="client-actions-item" onClick={() => select(onEditProfile)}>
            <Pencil size={14} aria-hidden="true" />
            {t('op.players.actions.editProfileLabel')}
          </button>
          <button type="button" role="menuitem" className="client-actions-item" onClick={() => select(onSetPin)}>
            <KeyRound size={14} aria-hidden="true" />
            {t('op.players.actions.pinLabel')}
          </button>
          <button
            type="button"
            role="menuitem"
            className={`client-actions-item${isActive ? ' is-danger' : ''}`}
            onClick={() => select(onToggleActive)}
          >
            {isActive ? <PowerOff size={14} aria-hidden="true" /> : <Power size={14} aria-hidden="true" />}
            {isActive ? t('op.players.actions.deactivateLabel') : t('op.players.actions.reactivateLabel')}
          </button>
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Прогнать — зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientActionsMenu.test.tsx`
Expected: 5/5 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientActionsMenu.tsx src/AFK4.Operator.App.Web/src/players/ClientActionsMenu.test.tsx
git commit -m "feat(players-s3): ClientActionsMenu — меню «⋯» (a11y kebab)"
```

---

## Task 9: `ClientDetail` — меню «⋯» вместо PIN-кнопки + баннер «деактивирован» + проброс

**Files:**
- Modify: `src/players/ClientDetail.tsx` (импорт, props, шапка, баннер)
- Test: `src/players/ClientDetail.test.tsx` (новые кейсы)

**Interfaces:**
- Consumes: `ClientActionsMenu` (Task 8); `client.status` (значение `'inactive'` для неактивного — из `playersModel`).
- Produces (новые props `ClientDetail`):
  - `canManageClient: boolean` — есть право `players.create` (гейт показа меню).
  - `onEditProfile: () => void`, `onToggleActive: () => void`.
  - Удаляются: `canSetPin` (заменён на `canManageClient`). `onSetPin` остаётся (зовётся пунктом меню).

> **Переходное состояние:** props ClientDetail меняются — оркестратор (Task 10) ещё не передаёт `canManageClient`/`onEditProfile`/`onToggleActive`, поэтому полный `bun run build` будет КРАСНЫМ до Task 10. Это ожидаемо. Гейт этой задачи — фокусные тесты компонента (`ClientDetail.test.tsx`), не полная сборка.

- [ ] **Step 1: Обновить тест** `ClientDetail.test.tsx` — добавить кейсы (по образцу существующих в файле; читать текущий тест на предмет фабрики props):

```tsx
  it('renders the actions menu when the staff can manage the client', () => {
    renderDetail({ canManageClient: true });
    expect(screen.getByRole('button', { name: 'Действия с клиентом' })).toBeInTheDocument();
  });

  it('hides the actions menu without manage permission', () => {
    renderDetail({ canManageClient: false });
    expect(screen.queryByRole('button', { name: 'Действия с клиентом' })).not.toBeInTheDocument();
  });

  it('shows the deactivated banner for an inactive client', () => {
    renderDetail({ client: { ...baseClient, status: 'inactive' } });
    expect(screen.getByText(/Клиент деактивирован/)).toBeInTheDocument();
  });
```

> Реализатор: подставить существующие `renderDetail`/`baseClient` из файла; если фабрика props не покрывает новые поля — расширить дефолты (`canManageClient: false`, `onEditProfile: () => {}`, `onToggleActive: () => {}`).

- [ ] **Step 2: Прогнать — падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: FAIL (props/меню/баннер ещё нет).

- [ ] **Step 3: Обновить `ClientDetail.tsx`**

Импорт (после строки 10):
```tsx
import { ClientActionsMenu } from './ClientActionsMenu';
```
Удалить неиспользуемый импорт `KeyRound` из строки 2 (`import { CalendarClock, KeyRound } from 'lucide-react';` → `import { CalendarClock } from 'lucide-react';`).

Props — заменить пару `canSetPin`/`onSetPin` (строки 48-49) на:
```tsx
  canManageClient: boolean;
  onSetPin: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
```

Шапка — заменить блок PIN-кнопки (строки 99-104) на меню:
```tsx
        {props.canManageClient && (
          <ClientActionsMenu
            isActive={client.status !== 'inactive'}
            onEditProfile={props.onEditProfile}
            onSetPin={props.onSetPin}
            onToggleActive={props.onToggleActive}
          />
        )}
```

Баннер — добавить сразу после `</header>` (после строки 114), перед `<div className="client-detail-chips">`:
```tsx
      {client.status === 'inactive' && (
        <div className="client-detail-banner" role="status">
          {t('op.players.detail.deactivatedBanner')}
        </div>
      )}
```

- [ ] **Step 4: Прогнать фокусные тесты — зелёные**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: PASS (старые + 3 новых).

> Полный `bun run build` сейчас КРАСНЫЙ (оркестратор ещё не даёт новые props) — это ожидаемо, восстановится в Task 10. НЕ гонять полную сборку здесь.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx
git commit -m "feat(players-s3): ClientDetail — меню «⋯» вместо PIN-кнопки + баннер деактивации"
```

---

## Task 10: Оркестратор — стейт, ветки, блокировка денег для неактивного, монтирование

**Files:**
- Modify: `src/BackendPlayersWorkspace.tsx`
- Test: `src/App.test.tsx` (smoke-кейс; ОТДЕЛЬНЫЙ прогон)

**Interfaces:**
- Consumes: `EditProfileModal` (T6), `ActiveStateConfirmModal` (T7), `apiClients.players.updateProfile/setActiveState/searchPlayers(...,includeInactive)` (T5), `ClientDetail` новые props (T9).
- Produces: ветки `runClientAction('updateProfile' | 'toggleActive', ...)`; флаги `canManageClient`; блокировка денежных действий при `selectedClient.status === 'inactive'`.

- [ ] **Step 1: Импорты** — добавить к строкам 26-28:

```tsx
import { EditProfileModal } from './players/EditProfileModal';
import { ActiveStateConfirmModal } from './players/ActiveStateConfirmModal';
```

- [ ] **Step 2: Расширить `PlayerActionId`** (строка 30):

```tsx
type PlayerActionId = 'topUp' | 'writeOffDebt' | 'buyPackage' | 'booking' | 'newCard' | 'correction' | 'refund' | 'setPin' | 'updateProfile' | 'toggleActive';
```

- [ ] **Step 3: Новый стейт** — добавить рядом с pin-стейтом (после строки 64):

```tsx
  const [editOpen, setEditOpen] = useState(false);
  const [editName, setEditName] = useState('');
  const [editPhone, setEditPhone] = useState('');
  const [activeStateOpen, setActiveStateOpen] = useState(false);
```

- [ ] **Step 4: Включить неактивных в поиск** — в эффекте загрузки списка (строка 81) передать `includeInactive=true`:

```tsx
        const players = await apiClients.players.searchPlayers(backend.branchId, clientSearch, 25, true);
```

- [ ] **Step 5: Флаг `canManageClient` + блокировка денег для неактивного.** После `canSetClientPin` (строка 279) добавить:

```tsx
  const isSelectedInactive = selectedClient !== null && selectedClient.status === 'inactive';
  const canManageClient = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.createPlayerAccount);
```

Затем у денежных/сессионных флагов добавить `&& !isSelectedInactive` — для `canPurchasePackage` (строка 247), `canTopUpWallet` (252), `canPayDebt` (258), `canCreateClientReservation` (264), `canManualCorrect` (269), `canRefundLedger` (274), `canSetClientPin` (279). Пример для `canTopUpWallet`:

```tsx
  const canTopUpWallet = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && !isSelectedInactive
    && hasPermission(backend.session, permissionNames.topUpWallet);
```

> `canManageClient` (правка/PIN/toggle) и `canCreatePlayer` НЕ получают `!isSelectedInactive` — правку профиля и реактивацию надо разрешать для неактивного клиента.

- [ ] **Step 6: Ветки `runClientAction`** — добавить перед финальным `else` (перед строкой 481):

```tsx
      } else if (id === 'updateProfile') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermEditProfile'));
        }

        const backendClient = requireSelectedBackendClient();
        const displayName = editName.trim();
        if (!displayName) {
          throw new Error(t('op.players.error.editNameRequired'));
        }

        const updated = await apiClients.players.updateProfile(nextBackend.branchId, backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          displayName,
          phoneNumber: editPhone.trim() || null
        });
        setClients((items) => items.map((c) => c.playerAccountId === backendClient.playerAccountId
          ? { ...c, name: updated.displayName, phoneNumber: updated.phoneNumber ?? '' }
          : c));
        setEditOpen(false);
      } else if (id === 'toggleActive') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermActiveState'));
        }

        const backendClient = requireSelectedBackendClient();
        const nextActive = backendClient.status === 'inactive';
        const updated = await apiClients.players.setActiveState(nextBackend.branchId, backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          isActive: nextActive
        });
        setClients((items) => items.map((c) => c.playerAccountId === backendClient.playerAccountId
          ? { ...c, status: updated.isActive ? 'active' : 'inactive', tone: updated.isActive ? 'active' : 'regular' }
          : c));
        setActiveStateOpen(false);
      }
```

- [ ] **Step 7: Открытие модалки правки с предзаполнением.** Добавить хелпер перед `return` (рядом с `bumpLedger`, ~строка 523):

```tsx
  const openEditProfile = () => {
    setEditName(selectedClient?.name ?? '');
    setEditPhone(selectedClient?.phoneNumber ?? '');
    setEditOpen(true);
  };
```

- [ ] **Step 8: Проброс в `ClientDetail`** — заменить пару `canSetPin`/`onSetPin` (строки 600-601) на:

```tsx
          canManageClient={canManageClient}
          onSetPin={() => setPinOpen(true)}
          onEditProfile={openEditProfile}
          onToggleActive={() => setActiveStateOpen(true)}
```

- [ ] **Step 9: Монтирование модалок** — добавить после pin-модалки (после строки 656):

```tsx
      {editOpen && (
        <EditProfileModal
          name={editName}
          phone={editPhone}
          onChangeName={setEditName}
          onChangePhone={setEditPhone}
          onClose={() => setEditOpen(false)}
          onSubmit={() => void runClientAction('updateProfile', t('op.players.actions.editProfileLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {activeStateOpen && (
        <ActiveStateConfirmModal
          mode={isSelectedInactive ? 'reactivate' : 'deactivate'}
          onClose={() => setActiveStateOpen(false)}
          onConfirm={() => void runClientAction('toggleActive', isSelectedInactive ? t('op.players.actions.reactivateLabel') : t('op.players.actions.deactivateLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}
```

- [ ] **Step 10: Собрать фронт — build снова зелёный**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: build PASS (props ClientDetail снова согласованы).

- [ ] **Step 11: Прогнать suite + App.test**

Run: `cd src/AFK4.Operator.App.Web && bun run test`
Expected: все subdir-тесты PASS.

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`
Expected: App.test PASS (89/89; новых регрессий нет). При желании реализатор добавляет smoke-кейс: на вкладке «Клиенты» с backend-клиентом видна кнопка меню «Действия с клиентом» — по образцу существующих players-кейсов в App.test.

- [ ] **Step 12: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(players-s3): оркестратор — правка/деактивация, блокировка денег для неактивного, includeInactive"
```

---

## Task 11: dev-mock — мутируемые клиенты, write-хендлеры, includeInactive, неактивная фикстура

**Files:**
- Modify: `src/devMockBackend.ts`

**Interfaces:**
- Consumes: эндпоинты `PATCH .../players/{id}`, `POST .../active-state`, `GET .../players?includeInactive=`.
- Produces: превью отражает правку имени/телефона и toggle активности; фикстура содержит одного неактивного клиента; поиск по умолчанию прячет неактивных, с `includeInactive=true` — показывает.

> Без этого превью (`bun run dev`) не покажет S3 (#14): деактивированный клиент должен реально пропадать/появляться и менять бейдж.

- [ ] **Step 1: Сделать `players` мутируемым + добавить неактивного.** Заменить функцию `players()` (строки 248-256) на мутируемое хранилище:

```ts
// Клиенты клуба для поиска: мутируемые (write-действия S3 меняют имя/активность), один неактивный.
type MockPlayer = { playerAccountId: string; displayName: string; phoneNumber: string; walletBalanceMinorUnits: number; debtBalanceMinorUnits: number; activePackageCount: number; isActive: boolean };
let mutablePlayers: MockPlayer[] | null = null;
function players(): MockPlayer[] {
  if (mutablePlayers === null) {
    mutablePlayers = [
      { playerAccountId: 'pl-1', displayName: 'Фариза Назарова', phoneNumber: '+992 93 100 20 30', walletBalanceMinorUnits: 45000, debtBalanceMinorUnits: 0, activePackageCount: 1, isActive: true },
      { playerAccountId: 'pl-2', displayName: 'Азиз Пиров', phoneNumber: '+992 90 555 22 11', walletBalanceMinorUnits: 12000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true },
      { playerAccountId: 'pl-3', displayName: 'Мадина Саидова', phoneNumber: '+992 98 700 11 22', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 3500, activePackageCount: 0, isActive: true },
      { playerAccountId: 'pl-4', displayName: 'Камрон Рахимов', phoneNumber: '+992 92 333 44 55', walletBalanceMinorUnits: 8000, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: true },
      { playerAccountId: 'pl-5', displayName: 'Дилноза Холова', phoneNumber: '+992 91 222 33 44', walletBalanceMinorUnits: 26000, debtBalanceMinorUnits: 0, activePackageCount: 2, isActive: true },
      { playerAccountId: 'pl-6', displayName: 'Бахром Сафаров', phoneNumber: '+992 93 444 55 66', walletBalanceMinorUnits: 0, debtBalanceMinorUnits: 0, activePackageCount: 0, isActive: false }
    ];
  }
  return mutablePlayers;
}
```

- [ ] **Step 2: Фильтр поиска учитывает `includeInactive`.** Заменить `filterPlayers` (строки 258-265):

```ts
function filterPlayers(query: string | null, includeInactive: boolean): MockPlayer[] {
  const q = (query ?? '').trim().toLowerCase();
  const digits = q.replace(/\D/g, '');
  return players().filter((p) => {
    if (!includeInactive && !p.isActive) return false;
    if (!q) return true;
    return p.displayName.toLowerCase().includes(q)
      || (digits.length > 0 && p.phoneNumber.replace(/\D/g, '').includes(digits));
  });
}
```

- [ ] **Step 3: Search-хендлер передаёт `includeInactive`.** Обновить ветку в `devMockFetch` (строки 384-386):

```ts
  if (url.pathname.endsWith('/players') && method === 'GET') {
    return json(filterPlayers(url.searchParams.get('query'), url.searchParams.get('includeInactive') === 'true'));
  }
```

- [ ] **Step 4: Write-хендлеры правки и active-state.** Добавить в `devMockFetch` перед общим `if (method !== 'GET') return noContent();` (перед строкой 447). PATCH `/players/{id}` — путь оканчивается на playerAccountId (guid), не на под-ресурс:

```ts
  if (url.pathname.endsWith('/active-state') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const id = url.pathname.split('/').slice(-2)[0];
    const player = players().find((p) => p.playerAccountId === id);
    if (player) player.isActive = Boolean(req.isActive);
    return json(player ?? {});
  }
  if (/\/players\/[^/]+$/.test(url.pathname) && method === 'PATCH') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const id = url.pathname.split('/').pop();
    const player = players().find((p) => p.playerAccountId === id);
    if (player) {
      if (typeof req.displayName === 'string') player.displayName = req.displayName;
      player.phoneNumber = typeof req.phoneNumber === 'string' ? req.phoneNumber : '';
    }
    return json(player ?? {});
  }
```

> Response мока — упрощённый объект клиента; фронт читает `displayName`/`phoneNumber`/`isActive` (остальные поля `PlayerAccountDto` оркестратор не использует в этих ветках).

- [ ] **Step 5: Проверить превью вручную (визуальный smoke)**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: build PASS (типы `MockPlayer` согласованы). Функциональную проверку превью (`bun run dev`) делает контролёр при желании — не блокирует.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/devMockBackend.ts
git commit -m "feat(players-s3): dev-mock — мутируемые клиенты, write-хендлеры правки/active-state, includeInactive, неактивная фикстура"
```

---

## Task 12: CSS baseline (меню/модалки/баннер/неактивная строка) + финал-проверка

**Files:**
- Modify: `src/players/ClientList.tsx` (класс `is-inactive` на строке)
- Modify: `src/styles/12-players.css`

**Interfaces:**
- Consumes: классы из T7-T9, T11 (`client-actions-menu/-trigger/-dropdown/-item`, `client-detail-banner`, `clients-edit-form`, `clients-confirm/-impact`, `clients-danger-action`, `panel-modal.danger`, `client-row.is-inactive`).

> `.clients-danger-action` и его hover/focus-visible уже добавлены в S2 (фикс a11y). Здесь — новые классы S3 + danger-tone модалки.

- [ ] **Step 1: Класс `is-inactive` на строке списка.** В `ClientList.tsx` (строка 84) добавить признак в className строки:

```tsx
              className={`client-row ${client.tone}${client.status === 'inactive' ? ' is-inactive' : ''}${client.playerAccountId === selectedClientId ? ' selected' : ''}`}
```

- [ ] **Step 2: Добавить CSS** в конец `src/styles/12-players.css` (использовать существующие токены `var(--...)`; свериться с соседними правилами в файле на предмет имён токенов — `--surface-*`, `--border-*`, `--text-*`, `--accent-*`, `--danger*`):

```css
/* S3 — меню «⋯», модалки правки/деактивации, баннер, неактивная строка */
.client-actions-menu {
  position: relative;
}

.client-actions-trigger {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  width: 32px;
  height: 32px;
  border: 1px solid var(--border-subtle);
  border-radius: 8px;
  background: var(--surface-raised);
  color: var(--text-secondary);
  cursor: pointer;
  transition: border-color 120ms ease, background 120ms ease, color 120ms ease;
}

.client-actions-trigger:hover:not(:disabled) {
  border-color: var(--border-accent);
  background: var(--surface-accent-soft);
  color: var(--accent-bright);
}

.client-actions-trigger:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: 2px;
}

.client-actions-dropdown {
  position: absolute;
  top: calc(100% + 6px);
  right: 0;
  z-index: 20;
  display: flex;
  flex-direction: column;
  min-width: 200px;
  padding: 6px;
  border: 1px solid var(--border-subtle);
  border-radius: 10px;
  background: var(--surface-overlay);
  box-shadow: 0 12px 32px rgba(0, 0, 0, 0.28);
}

.client-actions-item {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 8px 10px;
  border: none;
  border-radius: 7px;
  background: transparent;
  color: var(--text-primary);
  font: inherit;
  text-align: left;
  cursor: pointer;
  transition: background 120ms ease, color 120ms ease;
}

.client-actions-item:hover {
  background: var(--surface-accent-soft);
}

.client-actions-item:focus-visible {
  outline: 2px solid rgba(var(--accent-rgb), 0.82);
  outline-offset: -2px;
}

.client-actions-item.is-danger {
  color: var(--danger-text);
}

.client-actions-item.is-danger:hover {
  background: var(--danger-soft-bg);
}

.client-detail-banner {
  margin: 0 0 12px;
  padding: 10px 14px;
  border: 1px solid var(--danger);
  border-radius: 9px;
  background: var(--danger-soft-bg);
  color: var(--danger-text);
  font-size: 13px;
}

.clients-edit-form,
.clients-confirm {
  display: flex;
  flex-direction: column;
  gap: 10px;
}

.clients-confirm-impact {
  margin: 0;
  color: var(--text-secondary);
  font-size: 13px;
  line-height: 1.5;
}

.client-row.is-inactive {
  opacity: 0.62;
}

.client-row.is-inactive .client-row-status {
  color: var(--danger-text);
}

.panel-modal.danger .panel-modal-head {
  border-bottom-color: var(--danger);
}
```

> Если каких-то токенов (`--surface-overlay`, `--surface-accent-soft`, `--accent-rgb`, `--danger-soft-bg`, `--danger-text`) нет — свериться с `src/styles/` (общие токены `@afk4/tokens`) и заменить на существующий ближайший; не вводить хардкод-цвета (#29). `.clients-danger-action` уже определён (S2) — повторно не объявлять.

- [ ] **Step 3: ТРИ ГЕЙТА — финальная проверка слайса**

Run: `cd src/AFK4.Operator.App.Web && bun run test`
Expected: все subdir-тесты PASS.

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`
Expected: App.test PASS.

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: build PASS.

Run: `cd packages/i18n && bun test`
Expected: i18n guard PASS (parity + tg≠ru).

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~BillingEndpointTests"`
Expected: бэкенд-тесты PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientList.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "style(players-s3): baseline CSS меню/модалок/баннера + приглушение неактивной строки"
```

---

## Self-Review (контроль против спеки)

**Spec coverage** (`docs/superpowers/specs/2026-06-22-operator-clients-overhaul-design.md`, раздел «4. Правка профиля + деактивация (S3)»):
- ✅ Правка `PATCH /api/branches/{branchId}/players/{id}` с `UpdatePlayerAccountRequest(OrganizationId, DisplayName, PhoneNumber?)` — Task 1-2. Зеркалит create (`players.create`, org-match, audit).
- ✅ Деактивация/реактивация toggle `IsActive`, отдельный `POST .../active-state` — Task 1-2. Только soft (ledger/история живут — IsActive не трогает данные).
- ✅ UI: меню «⋯» (Править профиль · PIN · Деактивировать/Активировать) — Task 8-9. Drawer правки → решено как `PanelModal` (фактический паттерн после S2; спека допускала «drawer/модалки»). Confirm деактивации — Task 7. Неактивный помечен в списке/карточке (Task 9 баннер + Task 12 строка), денежные действия заблокированы (Task 10).
- ✅ Право `players.create` для всех трёх (Global Constraints) — спека §«Права».
- ✅ Сегмент «Неактивные» оживлён через `includeInactive` (Task 3) — иначе сегмент был бы вечно пустым (#32), а деактивация необратимой.

**Placeholder scan:** нет TODO/«fill in»/«similar to». Код приведён полностью для нового; модификации — точные диффы с номерами строк. Единственные условные развилки помечены явно (наличие `api.patch` в Task 5 Step 1; тип динамических i18n-ключей в Task 7 Step 4) с конкретной инструкцией.

**Type consistency:**
- Бэк: `UpdatePlayerAccountRequest`/`SetPlayerActiveStateRequest` — одинаковые имена/поля в Task 1 (контракт), Task 2 (эндпоинт), Task 5/иначе фронт-зеркало. `BillingCommandServiceResult.Missing` → 404 (подтверждено `ToHttpResult`).
- Фронт: `canManageClient` (новый props ClientDetail) — введён в Task 9, заполнен в Task 10. `onEditProfile`/`onToggleActive`/`onSetPin` — согласованы Task 8↔9↔10. `searchPlayers(...,includeInactive)` — сигнатура Task 5, вызов Task 10, мок Task 11.
- `PlayerActionId` расширен в Task 10 (`'updateProfile'|'toggleActive'`), ветки там же.
- `client.status === 'inactive'` / `tone: 'regular'` — консистентно с `projectPlayerClient` (operatorHelpers) и `matchesSegment` (playersModel).

**Известные переходные состояния:** между Task 9 и Task 10 полный `bun run build` КРАСНЫЙ (props ClientDetail рассогласованы) — ожидаемо, гейт Task 9 = фокусные тесты, Task 10 Step 10 восстанавливает build.
