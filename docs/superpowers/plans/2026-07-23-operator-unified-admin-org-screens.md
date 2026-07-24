# Оператор — Org-экраны (секция «Сеть») Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать владельцу сети в operator-app новую owner-ориентированную секцию «Сеть» с четырьмя org-level экранами: Branches (свод по филиалам), Billing (подписка read-only), Install (установщик-Мастер), Journal (org-level аудит).

**Architecture:** Новая секция рейла `network` по паттерну существующей секции `management`: `networkNav.ts` (destinations + permission-гейт `hasAnyPermission`) + хост `NetworkWorkspace.tsx` (destination-switcher, мирроринг `ManagementWorkspace`) + 4 экрана-destination, каждый самозагружается через `createAuthenticatedOperatorClients(backend.config, backend.session)`. На бэке — новый org-level эндпоинт чтения аудита, корректное право `branches.view`, аудит-запись у `install.discover`.

**Tech Stack:** Backend — .NET 10 minimal-API, EF Core, xUnit через `PlatformApiFactory`. Frontend — React/TS, `bun test` (happy-dom + jest-dom), build `tsc -b && vite build`. UI — операторские CSS-атомы `.ui-*` + `management/kit/` (`MgmtTable`/`MgmtDrawer`/`RowActionsMenu`) + `operatorPrimitives` (`EmptyState`/`Skeleton`/`Money`/`CriticalActionConfirmation`) + `ManagementScreen` + `PanelModal`. i18n — `@afk4/i18n`, деньги — `formatMinorUnits`/`<Money>` из `@afk4/money`.

## Global Constraints

- **Ветка:** `feat/operator-unified-admin-org-screens` (спека закоммичена там, `aace5957`). Не начинать на main.
- **Никаких AI-подписей** в коммитах/коде/PR (`Co-Authored-By: Claude`, `Generated with` и т.п. запрещены).
- **money-путь:** суммы в minor units; на UI-границе `minorToMajor` (через `formatMinorUnits`/`<Money>`), НЕ форматировать minor units как major.
- **Контракт `StaffSignInResponse` НЕ менять** (Platform.Web не должен сломаться).
- **Org-level авторизация:** `RequireOrganizationPermission(permission)` (синхронный) + ручной IDOR-guard `organizationId != authorization.StaffContext!.OrganizationId → 403`. Данные читать по `StaffContext.OrganizationId`, не по параметру URL.
- **Точные строки прав (backend `StaffPermissionNames`):** `ViewSubscription = "billing.subscription.view"`, `InstallDevice = "devices.install"`, `ViewAudit = "audit.view"`, новое `ViewBranches = "branches.view"`.
- **i18n:** новые ключи добавлять в `locales/{ru,en,tg}.json`, затем `bun run gen` в `packages/i18n`. Guard-тест требует идентичный набор ключей во всех трёх локалях И `tg !== ru` (кроме легитимных заимствований в `TG_IDENTICAL_TO_RU_ALLOWED`). Давать НАСТОЯЩИЙ таджикский перевод (best-effort, как принято в проекте — не native-reviewed), НЕ копировать ru ради зелёного guard (#37). Заимствования, реально совпадающие с ru (напр. «Клуб»), добавлять в allowlist с обоснованием.
- **Тесты фронта:** сеть мокается через `mock.module('../../operatorHelpers', ...)` подменой `createAuthenticatedOperatorClients` (НЕ global fetch). Обёртка рендера — `<I18nProvider initialLocale="ru"><ToastProvider>…</ToastProvider></I18nProvider>`.
- **Команды фронта (из `src/AFK4.Operator.App.Web`):** тест — `bun test <path>`; полный прогон — `bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && bun test src/App.test.tsx`; сборка — `bun run build` (`tsc -b && vite build`, тайпчекает и тест-файлы).
- **Команды бэка:** `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter <...>`.
- **Финал каждого фронт-таска обязан включать `bun run build`** (зелёный `bun test` ≠ зелёная сборка — `tsc` тайпчекает тест-файлы и сужения).

---

## Файловая структура

**Backend (создать/изменить):**
- Modify `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs` — новая константа `ViewBranches`.
- Modify `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` — `ViewBranches` в роль Owner.
- Modify `src/AFK4.Platform.Api/Endpoints/NewsEndpoints.cs` — `/api/owner/branches` с `ViewBranches` вместо `ManageNews`.
- Modify `src/AFK4.Platform.Api/Audit/IAuditSearchService.cs` — метод `SearchOrganizationAsync`.
- Modify `src/AFK4.Platform.Api/Audit/EfAuditSearchService.cs` — реализация `SearchOrganizationAsync`.
- Modify `src/AFK4.Platform.Api/Endpoints/UpdateEndpoints.cs` — новый `GET /api/organizations/{organizationId}/audit`.
- Modify `src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs` — аудит у `/api/install/auth/discover`.

**Frontend (создать):**
- `src/AFK4.Operator.App.Web/src/network/networkNav.ts`
- `src/AFK4.Operator.App.Web/src/network/NetworkWorkspace.tsx`
- `src/AFK4.Operator.App.Web/src/network/branches/{BranchesDestination.tsx, useBranchRollup.ts, branchRollupModel.ts, RenameBranchModal.tsx}`
- `src/AFK4.Operator.App.Web/src/network/billing/{BillingDestination.tsx, useBilling.ts, billingModel.ts}`
- `src/AFK4.Operator.App.Web/src/network/install/{InstallDestination.tsx, installModel.ts}`
- `src/AFK4.Operator.App.Web/src/network/journal/{JournalDestination.tsx, useOrgAudit.ts, orgAuditModel.ts, OrgAuditFilters.tsx, dateRange.ts}`
- `src/AFK4.Operator.App.Web/src/api/clients/{orgBranches.ts, orgBilling.ts, orgAudit.ts}`

**Frontend (изменить):**
- `src/AFK4.Operator.App.Web/src/permissionNames.ts` — `viewSubscription`, `installDevice`, `viewBranches`.
- `src/AFK4.Operator.App.Web/src/operatorTypes.ts` — `WorkspaceId` += `'network'`.
- `src/AFK4.Operator.App.Web/src/operatorData.ts` — navSections += секция `network`.
- `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — `workspacePermissionRules.network`.
- `src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx` — рендер `NetworkWorkspace`.
- `src/AFK4.Operator.App.Web/src/api/clients/index.ts` — регистрация новых клиентов.
- `src/AFK4.Operator.App.Web/src/operatorConfig.ts` — optional `setupInstallerUrl`.
- `src/AFK4.Operator.App.Web/src/devHostBridge.ts` — dev-инжект `setupInstallerUrl` (по надобности).
- `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts` — новые ключи + presence-блок.

---

## Task 1: Backend — право `branches.view` + ре-гейт `/api/owner/branches`

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/NewsEndpoints.cs` (эндпоинт `/api/owner/branches`, ~строки 29-44)
- Test: `tests/AFK4.Platform.Api.Tests/OwnerBranchesEndpointTests.cs` (создать)

**Interfaces:**
- Produces: `StaffPermissionNames.ViewBranches` (значение `"branches.view"`), которым Task 5 гейтит операторский Branches-экран (operator-константа `permissionNames.viewBranches`).

**Контекст:** `ManageNews = "news.manage"` — Owner-эксклюзивное право (проверено: единственная роль с `ManageNews` — Owner). `/api/owner/branches` сейчас защищён `ManageNews` (заглушка). Единственные потребители эндпоинта — operator news-клиент (`src/AFK4.Operator.App.Web/src/api/clients/news.ts:37`) и тест `OwnerNewsEndpointsTests.cs`, оба под Owner. Ре-гейт на `ViewBranches` (тоже даём Owner) поведение не меняет, но убирает misuse (#35).

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/OwnerBranchesEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.News;

namespace AFK4.Platform.Api.Tests;

public sealed class OwnerBranchesEndpointTests
{
    [Fact]
    public async Task GET_owner_branches_as_owner_returns_org_branches()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var branches = await client.GetFromJsonAsync<OwnerBranchSummaryDto[]>("/api/owner/branches");

        Assert.NotNull(branches);
        Assert.Contains(branches!, b => b.BranchId == TestIds.BranchId);
    }

    [Fact]
    public async Task GET_owner_branches_as_cashier_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync("/api/owner/branches");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OwnerBranchesEndpointTests`
Expected: оба теста FAIL (эндпоинт ещё под `ManageNews`; owner имеет `ManageNews` → первый тест мог бы пройти, но константы `ViewBranches` ещё нет — компиляция теста проходит, но после Step 3 гейт меняется; на этом шаге тест валиден как регресс). Если оба зелёные до правок — это ок для первого (owner имеет оба), но второй (cashier 403) должен быть зелёным и сейчас. Основная проверка Step 4 — что после ре-гейта оба зелёные.

- [ ] **Step 3: Добавить константу + право в роль + ре-гейт эндпоинта**

В `StaffPermissionNames.cs` добавить константу (рядом с `ManageBranchSettings`):

```csharp
    public const string ManageBranchSettings = "branches.settings.manage";
    // Owner-only: view the org-wide branch roster (network overview).
    public const string ViewBranches = "branches.view";
```

В `PermissionCatalog.cs` внутрь `HashSet<string>` роли `Owner` (до закрывающей `}` набора Owner) добавить:

```csharp
                StaffPermissionNames.ManageBranchSettings,
                StaffPermissionNames.ViewBranches,
                StaffPermissionNames.ManagePaymentGateways,
```

В `NewsEndpoints.cs` заменить право в `/api/owner/branches`:

```csharp
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewBranches);
```

- [ ] **Step 4: Прогнать — убедиться, что зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~OwnerBranchesEndpointTests|FullyQualifiedName~OwnerNewsEndpointsTests"`
Expected: PASS (owner видит филиалы; cashier — 403; News-регресс зелёный, т.к. Owner имеет и `ManageNews`, и `ViewBranches`).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs src/AFK4.Platform.Api/Identity/PermissionCatalog.cs src/AFK4.Platform.Api/Endpoints/NewsEndpoints.cs tests/AFK4.Platform.Api.Tests/OwnerBranchesEndpointTests.cs
git commit -m "feat(api): право branches.view + ре-гейт /api/owner/branches с news.manage"
```

---

## Task 2: Backend — org-level чтение аудита `GET /api/organizations/{id}/audit`

**Files:**
- Modify: `src/AFK4.Platform.Api/Audit/IAuditSearchService.cs`
- Modify: `src/AFK4.Platform.Api/Audit/EfAuditSearchService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/UpdateEndpoints.cs` (добавить эндпоинт рядом с существующим `/api/branches/{branchId}/audit`)
- Test: `tests/AFK4.Platform.Api.Tests/OrganizationAuditEndpointTests.cs` (создать)

**Interfaces:**
- Consumes: `AuditSearchQuery` (в `AFK4.Platform.Api.Audit`), `AuditSearchResultDto`/`AuditRecordDto` (в `AFK4.Shared.Contracts.Audit`), `StaffAuthorizationService.RequireOrganizationPermission`, `WriteAuditAsync` (в `EndpointHelpers.Audit.cs`), `AuditActionNames.ViewAudit`.
- Produces: маршрут `GET /api/organizations/{organizationId:guid}/audit`, потребляемый Task 8 (operator `orgAudit` клиент).

**Контекст:** существующий branch-эндпоинт фильтрует `OrganizationId == organizationId && BranchId == branchId`. Org-версия фильтрует только `OrganizationId == organizationId` → включает все филиалы + записи с `BranchId == null` (org-level действия owner'а, которые сейчас недоступны для чтения).

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/OrganizationAuditEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Data.Entities;
using AFK4.Shared.Contracts.Audit;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class OrganizationAuditEndpointTests
{
    private static async Task SeedAuditAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var at = DateTimeOffset.Parse("2026-07-20T10:00:00Z");
        // branch-scoped record
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Action = "loyalty.settings.updated",
            TargetType = "LoyaltySettings",
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = at
        });
        // org-level record (BranchId == null) — currently unreadable via any endpoint
        db.AuditRecords.Add(new AuditRecordEntity
        {
            AuditRecordId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = null,
            Action = "news.published",
            TargetType = "News",
            Outcome = AuditOutcome.Succeeded,
            SourceApp = "PlatformApi",
            DetailsJson = "{}",
            CreatedAtUtc = at.AddMinutes(1)
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task GET_org_audit_as_owner_includes_branch_and_org_level_records()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedAuditAsync(factory);

        var result = await client.GetFromJsonAsync<AuditSearchResultDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.NotNull(result);
        Assert.Contains(result!.Records, r => r.Action == "loyalty.settings.updated");
        Assert.Contains(result.Records, r => r.Action == "news.published" && r.BranchId == null);
    }

    [Fact]
    public async Task GET_org_audit_rejects_other_org_with_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_org_audit_as_cashier_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.CashierOperator);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/audit");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
```

Примечание для реализатора: имена сущности/полей (`AuditRecordEntity`, `AuditOutcome.Succeeded`) сверить с существующим кодом записи аудита (`AuditRecordStager`/`AuditRecordEntity`); если поле называется иначе — подставить фактическое. Модель entity уже используется в `AuditSearchEndpointTests.cs` — взять её как образец сидирования.

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OrganizationAuditEndpointTests`
Expected: FAIL — маршрут `/api/organizations/{id}/audit` не существует (404).

- [ ] **Step 3: Добавить метод сервиса**

В `IAuditSearchService.cs`:

```csharp
public interface IAuditSearchService
{
    Task<AuditSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        AuditSearchQuery query,
        CancellationToken cancellationToken);

    Task<AuditSearchResultDto> SearchOrganizationAsync(
        Guid organizationId,
        AuditSearchQuery query,
        CancellationToken cancellationToken);
}
```

В `EfAuditSearchService.cs` добавить метод (рефакторинг: вынести общую фильтрацию/проекцию в приватный хелпер, чтобы не дублировать — DRY). Полная замена файла тела класса:

```csharp
    public Task<AuditSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId && record.BranchId == branchId);
        return ExecuteAsync(records, query, cancellationToken);
    }

    public Task<AuditSearchResultDto> SearchOrganizationAsync(
        Guid organizationId,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var records = dbContext.AuditRecords
            .AsNoTracking()
            .Where(record => record.OrganizationId == organizationId);
        return ExecuteAsync(records, query, cancellationToken);
    }

    private static async Task<AuditSearchResultDto> ExecuteAsync(
        IQueryable<AuditRecordEntity> records,
        AuditSearchQuery query,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit ?? DefaultLimit, 1, MaxLimit);
        var action = Normalize(query.Action);
        var outcome = Normalize(query.Outcome);
        var targetType = Normalize(query.TargetType);

        if (action is not null) records = records.Where(record => record.Action == action);
        if (outcome is not null) records = records.Where(record => record.Outcome == outcome);
        if (targetType is not null) records = records.Where(record => record.TargetType == targetType);
        if (query.FromUtc.HasValue) records = records.Where(record => record.CreatedAtUtc >= query.FromUtc.Value);
        if (query.ToUtc.HasValue) records = records.Where(record => record.CreatedAtUtc <= query.ToUtc.Value);
        if (query.ActorStaffUserId.HasValue) records = records.Where(record => record.ActorStaffUserId == query.ActorStaffUserId.Value);
        if (query.MinAmountMinorUnits.HasValue)
            records = records.Where(record => record.AmountMinorUnits != null && record.AmountMinorUnits >= query.MinAmountMinorUnits.Value);
        if (query.MaxAmountMinorUnits.HasValue)
            records = records.Where(record => record.AmountMinorUnits != null && record.AmountMinorUnits <= query.MaxAmountMinorUnits.Value);

        var result = await records
            .OrderByDescending(record => record.CreatedAtUtc)
            .ThenByDescending(record => record.AuditRecordId)
            .Take(limit)
            .Select(record => new AuditRecordDto(
                record.AuditRecordId,
                record.OrganizationId,
                record.BranchId,
                record.ActorStaffUserId,
                record.Action,
                record.TargetType,
                record.TargetId,
                record.Outcome,
                record.SourceApp,
                record.DetailsJson,
                record.CreatedAtUtc)
            {
                ActorPlatformAdminUserId = record.ActorPlatformAdminUserId,
                AmountMinorUnits = record.AmountMinorUnits
            })
            .ToListAsync(cancellationToken);

        return new AuditSearchResultDto(result, limit);
    }
```

(Оставить существующие `private const int DefaultLimit`/`MaxLimit` и `Normalize`. Импорт entity-типа `AuditRecordEntity` — как в текущем файле; сверить точное имя типа сущности `AuditRecords`.)

- [ ] **Step 4: Добавить эндпоинт**

В `UpdateEndpoints.cs`, сразу после регистрации `GET /api/branches/{branchId:guid}/audit`, добавить:

```csharp
        app.MapGet("/api/organizations/{organizationId:guid}/audit", async (
            Guid organizationId,
            string? action,
            string? outcome,
            string? targetType,
            DateTimeOffset? fromUtc,
            DateTimeOffset? toUtc,
            int? limit,
            Guid? actorStaffUserId,
            long? minAmount,
            long? maxAmount,
            StaffAuthorizationService authorizationService,
            IAuditRecordWriter auditRecordWriter,
            IAuditSearchService auditSearchService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewAudit);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var query = new AuditSearchQuery(action, outcome, targetType, fromUtc, toUtc, limit, actorStaffUserId, minAmount, maxAmount);
            var result = await auditSearchService.SearchOrganizationAsync(
                authorization.StaffContext.OrganizationId,
                query,
                cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                authorization.StaffContext.OrganizationId,
                Guid.Empty,
                authorization.StaffContext.StaffUserId,
                AuditActionNames.ViewAudit,
                "AuditRecord",
                null,
                AuditOutcome.Succeeded,
                new { Scope = "organization", Count = result.Records.Count, result.Limit, action, outcome, targetType, fromUtc, toUtc },
                cancellationToken);

            return Results.Ok(result);
        });
```

Примечание: `WriteAuditAsync` требует `branchId` (non-nullable `Guid`) — для org-scope передаём `Guid.Empty` как sentinel «org-level запись аудита о просмотре». Если реализатор увидит, что `AuditRecordWriteRequest.BranchId` — nullable и хелпер это поддерживает через перегрузку, предпочесть `null`; иначе `Guid.Empty` приемлемо (это запись о чтении, не о доменном действии). Сверить, какой вариант чист.

- [ ] **Step 5: Прогнать — убедиться, что зелёные**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~OrganizationAuditEndpointTests`
Expected: PASS (3 теста).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Audit/IAuditSearchService.cs src/AFK4.Platform.Api/Audit/EfAuditSearchService.cs src/AFK4.Platform.Api/Endpoints/UpdateEndpoints.cs tests/AFK4.Platform.Api.Tests/OrganizationAuditEndpointTests.cs
git commit -m "feat(api): org-level чтение аудита GET /api/organizations/{id}/audit"
```

---

## Task 3: Backend — аудит-запись у `/api/install/auth/discover`

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs` (эндпоинт `POST /api/install/auth/discover`, ~строки 153-164)
- Test: `tests/AFK4.Platform.Api.Tests/InstallDiscoverAuditTests.cs` (создать)

**Interfaces:**
- Consumes: `WriteAuditAsync`, `AuditActionNames.InstallDiscoverInvoked` (`= "install.discover.invoked"`), `IAuditRecordWriter`.

**Контекст:** `discover` — единственный install-эндпоинт без аудита; константа `InstallDiscoverInvoked` объявлена, но не используется. Закрываем пробел write-path. `discover` org-level (`RequireOrganizationPermission(InstallDevice)`) — пишем с `BranchId = Guid.Empty` (org-scope, конкретного филиала нет).

- [ ] **Step 1: Написать падающий тест**

Создать `tests/AFK4.Platform.Api.Tests/InstallDiscoverAuditTests.cs`:

```csharp
using System.Net.Http.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class InstallDiscoverAuditTests
{
    [Fact]
    public async Task POST_install_discover_writes_audit_record()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);

        var response = await client.PostAsync("/api/install/auth/discover", null);
        response.EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var record = await db.AuditRecords.SingleAsync(r => r.Action == AuditActionNames.InstallDiscoverInvoked);
        Assert.Equal(TestIds.OrganizationId, record.OrganizationId);
        Assert.Equal(AuditOutcome.Succeeded, record.Outcome);
    }
}
```

- [ ] **Step 2: Прогнать — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~InstallDiscoverAuditTests`
Expected: FAIL — `SingleAsync` не находит запись (discover не пишет аудит).

- [ ] **Step 3: Добавить аудит-запись**

В `DeviceEndpoints.cs`, эндпоинт `discover` — добавить `IAuditRecordWriter` в параметры делегата и запись после успешного `discover`:

```csharp
        app.MapPost("/api/install/auth/discover", async (
            StaffAuthorizationService authorizationService,
            IInstallService installService,
            IAuditRecordWriter auditRecordWriter,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.InstallDevice);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);
            var staff = authorization.StaffContext!;
            var result = await installService.DiscoverForStaffAsync(staff.OrganizationId, staff.BranchIds, staff.DisplayName, cancellationToken);

            await WriteAuditAsync(
                auditRecordWriter,
                staff.OrganizationId,
                Guid.Empty,
                staff.StaffUserId,
                AuditActionNames.InstallDiscoverInvoked,
                "Install",
                null,
                AuditOutcome.Succeeded,
                new { BranchCount = staff.BranchIds.Count },
                cancellationToken);

            return ToInstallHttpResult(result);
        });
```

- [ ] **Step 4: Прогнать — убедиться, что зелёный**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~InstallDiscoverAuditTests`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Endpoints/DeviceEndpoints.cs tests/AFK4.Platform.Api.Tests/InstallDiscoverAuditTests.cs
git commit -m "fix(api): аудит install.discover (закрыт пробел write-path)"
```

---

## Task 4: Frontend — каркас секции «Сеть» (nav + права + хост + плейсхолдеры)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/permissionNames.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorTypes.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorData.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts`
- Modify: `src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx`
- Create: `src/AFK4.Operator.App.Web/src/network/networkNav.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/NetworkWorkspace.tsx`
- Create: `src/AFK4.Operator.App.Web/src/network/branches/BranchesDestination.tsx` (плейсхолдер)
- Create: `src/AFK4.Operator.App.Web/src/network/billing/BillingDestination.tsx` (плейсхолдер)
- Create: `src/AFK4.Operator.App.Web/src/network/install/InstallDestination.tsx` (плейсхолдер)
- Create: `src/AFK4.Operator.App.Web/src/network/journal/JournalDestination.tsx` (плейсхолдер)
- Modify: `locales/{ru,en,tg}.json`
- Modify: `packages/i18n/src/messages.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/networkNav.test.ts` (создать)
- Test: `src/AFK4.Operator.App.Web/src/operatorVisibility.test.ts` (дополнить)

**Interfaces:**
- Produces:
  - `permissionNames.viewSubscription = 'billing.subscription.view'`, `permissionNames.installDevice = 'devices.install'`, `permissionNames.viewBranches = 'branches.view'`.
  - `type NetworkDestinationId = 'branches' | 'billing' | 'install' | 'journal'`.
  - `networkDestinations: NetworkDestination[]`, `allowedNetworkDestinations(session): NetworkDestination[]`.
  - `WorkspaceId` включает `'network'`.
  - `NetworkWorkspace({ backend }: { backend: OperatorBackendContext | null })`.
  - Экраны-destination принимают проп `{ backend: OperatorBackendContext | null }` (Task 5-8 наполняют их).
- Consumes: `OperatorBackendContext` — тот же тип, что использует `management/destinations/types.ts` (`DestinationProps.backend`); импортировать из того же источника. `hasAnyPermission` из `operatorPermissions`. `ManagementScreen`, `EmptyState` из существующих модулей.

- [ ] **Step 1: Дополнить operator permission-каталог**

В `src/AFK4.Operator.App.Web/src/permissionNames.ts` добавить в объект `permissionNames` (рядом с `viewAudit`):

```ts
  viewAudit: 'audit.view',
  viewSubscription: 'billing.subscription.view',
  installDevice: 'devices.install',
  viewBranches: 'branches.view',
```

- [ ] **Step 2: Добавить `'network'` в WorkspaceId**

В `src/AFK4.Operator.App.Web/src/operatorTypes.ts`:

```ts
export type WorkspaceId = 'map' | 'dashboard' | 'booking' | 'cash' | 'players' | 'logs' | 'management' | 'stock' | 'network';
```

- [ ] **Step 3: Создать `networkNav.ts`**

`src/AFK4.Operator.App.Web/src/network/networkNav.ts`:

```ts
import type { LucideIcon } from 'lucide-react';
import { Building2, CreditCard, MonitorDown, ScrollText } from 'lucide-react';
import type { MessageKey } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import { hasAnyPermission } from '../operatorPermissions';
import { permissionNames } from '../permissionNames';

export type NetworkDestinationId = 'branches' | 'billing' | 'install' | 'journal';

export interface NetworkDestination {
  id: NetworkDestinationId;
  labelKey: MessageKey;
  subtitleKey: MessageKey;
  Icon: LucideIcon;
  permissions: readonly string[]; // visible if the session has ANY of these
}

export const networkDestinations: readonly NetworkDestination[] = [
  {
    id: 'branches',
    labelKey: 'op.network.dest.branches',
    subtitleKey: 'op.network.dest.branches.subtitle',
    Icon: Building2,
    permissions: [permissionNames.viewBranches]
  },
  {
    id: 'billing',
    labelKey: 'op.network.dest.billing',
    subtitleKey: 'op.network.dest.billing.subtitle',
    Icon: CreditCard,
    permissions: [permissionNames.viewSubscription]
  },
  {
    id: 'install',
    labelKey: 'op.network.dest.install',
    subtitleKey: 'op.network.dest.install.subtitle',
    Icon: MonitorDown,
    permissions: [permissionNames.installDevice]
  },
  {
    id: 'journal',
    labelKey: 'op.network.dest.journal',
    subtitleKey: 'op.network.dest.journal.subtitle',
    Icon: ScrollText,
    permissions: [permissionNames.viewAudit]
  }
];

export function allowedNetworkDestinations(session: OperatorAuthSession | null): NetworkDestination[] {
  return networkDestinations.filter((destination) => hasAnyPermission(session, destination.permissions));
}
```

- [ ] **Step 4: Написать падающий тест nav**

`src/AFK4.Operator.App.Web/src/network/networkNav.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import type { OperatorAuthSession } from '../authClient';
import { allowedNetworkDestinations } from './networkNav';

function sessionWith(permissions: string[]): OperatorAuthSession {
  return { permissions } as OperatorAuthSession;
}

describe('networkNav', () => {
  it('owner (all org perms) sees all four destinations', () => {
    const ids = allowedNetworkDestinations(
      sessionWith(['branches.view', 'billing.subscription.view', 'devices.install', 'audit.view'])
    ).map((d) => d.id);
    expect(ids).toEqual(['branches', 'billing', 'install', 'journal']);
  });

  it('a session with only audit.view sees just journal', () => {
    const ids = allowedNetworkDestinations(sessionWith(['audit.view'])).map((d) => d.id);
    expect(ids).toEqual(['journal']);
  });

  it('a session with no org perms sees nothing', () => {
    expect(allowedNetworkDestinations(sessionWith(['sessions.start']))).toEqual([]);
  });
});
```

- [ ] **Step 5: Прогнать — убедиться, что падает**

Run: `bun test src/network/networkNav.test.ts`
Expected: FAIL (модуль `networkNav` уже есть после Step 3 → тест должен пройти; если `hasAnyPermission`/`permissionNames` ещё не содержат новые ключи — упадёт на импорте/значениях). Основная цель — зелёный после Step 1+3.

- [ ] **Step 6: Добавить workspace-правило + navSection**

В `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — добавить импорт и правило:

```ts
import { networkDestinations } from './network/networkNav';
// ...
export const workspacePermissionRules: Record<WorkspaceId, readonly string[]> = {
  // ...существующие...
  network: [...new Set(networkDestinations.flatMap((destination) => destination.permissions))]
};
```

В `src/AFK4.Operator.App.Web/src/operatorData.ts` — добавить импорт иконки и секцию в конец `navSections`:

```ts
import { Network } from 'lucide-react';
// ...
  {
    key: 'network',
    labelKey: 'op.shell.navGroup.network',
    icon: Network,
    items: [{ id: 'network', labelKey: 'op.shell.navGroup.network' }]
  }
```

- [ ] **Step 7: Создать хост `NetworkWorkspace.tsx` + 4 плейсхолдера**

`src/AFK4.Operator.App.Web/src/network/NetworkWorkspace.tsx` (мирроринг destination-switcher из `ManagementWorkspace`; каждый экран самозагружается — общей загрузки данных на уровне хоста нет):

```tsx
import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../management/destinations/types';
import { allowedNetworkDestinations, type NetworkDestinationId } from './networkNav';
import { BranchesDestination } from './branches/BranchesDestination';
import { BillingDestination } from './billing/BillingDestination';
import { InstallDestination } from './install/InstallDestination';
import { JournalDestination } from './journal/JournalDestination';

export function NetworkWorkspace({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  const session = backend?.session ?? null;
  const destinations = useMemo(() => allowedNetworkDestinations(session), [session]);
  const [active, setActive] = useState<NetworkDestinationId | null>(destinations[0]?.id ?? null);

  if (destinations.length === 0) {
    return (
      <section className="workspace-screen management-screen">
        <div className="management-screen-body">
          <EmptyState title={t('op.network.noAccess')} />
        </div>
      </section>
    );
  }

  const currentId: NetworkDestinationId = destinations.some((d) => d.id === active)
    ? (active as NetworkDestinationId)
    : destinations[0].id;

  function renderActive(): JSX.Element {
    switch (currentId) {
      case 'branches': return <BranchesDestination backend={backend} />;
      case 'billing': return <BillingDestination backend={backend} />;
      case 'install': return <InstallDestination backend={backend} />;
      case 'journal': return <JournalDestination backend={backend} />;
    }
  }

  return (
    <div className="management-workspace">
      <nav className="management-nav" aria-label={t('op.shell.navGroup.network')}>
        {destinations.map((destination) => {
          const { Icon } = destination;
          return (
            <button
              key={destination.id}
              type="button"
              className={`management-nav-item${destination.id === currentId ? ' active' : ''}`}
              aria-current={destination.id === currentId}
              onClick={() => setActive(destination.id)}
            >
              <Icon size={18} aria-hidden="true" />
              <span className="management-nav-label">{t(destination.labelKey)}</span>
              <span className="management-nav-subtitle">{t(destination.subtitleKey)}</span>
            </button>
          );
        })}
      </nav>
      <div className="management-active-pane">{renderActive()}</div>
    </div>
  );
}
```

Примечание: классы `management-workspace`/`management-nav`/`management-nav-item`/`management-active-pane` переиспользуются из существующего `ManagementWorkspace` (стили `23-management-crud.css`/`15-settings.css`). Сверить фактические имена классов в `ManagementWorkspace.tsx` и использовать те же (если там `management-nav-item` называется иначе — взять фактическое).

Четыре плейсхолдера (каждый — минимальный экран через `ManagementScreen`, наполнится в Task 5-8). Пример `src/AFK4.Operator.App.Web/src/network/branches/BranchesDestination.tsx`:

```tsx
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import type { OperatorBackendContext } from '../../management/destinations/types';

export function BranchesDestination({ backend: _backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  return (
    <ManagementScreen title={t('op.network.dest.branches')} subtitle={t('op.network.dest.branches.subtitle')} contentWidth="full">
      <EmptyState title={t('op.network.placeholder')} />
    </ManagementScreen>
  );
}
```

Аналогично `BillingDestination.tsx` (`op.network.dest.billing`), `InstallDestination.tsx` (`op.network.dest.install`), `JournalDestination.tsx` (`op.network.dest.journal`) — те же 6 строк с соответствующими ключами.

- [ ] **Step 8: Подключить в WorkspaceRouter**

В `src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx` добавить импорт и ветку (рядом с `management`):

```tsx
import { NetworkWorkspace } from './network/NetworkWorkspace';
// ...
      {workspace === 'network' && <NetworkWorkspace backend={backend} />}
```

- [ ] **Step 9: Добавить i18n-ключи**

В `locales/ru.json`, `locales/en.json`, `locales/tg.json` добавить (значения ru / en / tg):

```
op.shell.navGroup.network       "Сеть" / "Network" / "Шабака"
op.network.dest.branches        "Филиалы" / "Branches" / "Филиалҳо"
op.network.dest.branches.subtitle "Свод по сети" / "Network overview" / "Шарҳи шабака"
op.network.dest.billing         "Подписка" / "Subscription" / "Обунашавӣ"
op.network.dest.billing.subtitle "Тариф и счета" / "Plan and invoices" / "Таъриф ва ҳисобҳо"
op.network.dest.install         "Установка" / "Install" / "Насб"
op.network.dest.install.subtitle "Подключить новый ПК" / "Onboard a new PC" / "Пайваст кардани ПК-и нав"
op.network.dest.journal         "Журнал" / "Journal" / "Журнал"
op.network.dest.journal.subtitle "Аудит действий" / "Action audit" / "Аудити амалҳо"
op.network.noAccess             "Нет доступа к разделу «Сеть»" / "No access to the Network section" / "Дастрасӣ ба бахши «Шабака» нест"
op.network.placeholder          "Скоро" / "Coming soon" / "Ба зудӣ"
```

Заметка по tg: «Журнал» — заимствование, совпадает с ru → добавить `op.network.dest.journal` в `TG_IDENTICAL_TO_RU_ALLOWED` в `messages.test.ts` (обоснование: журнал — устоявшееся заимствование). Остальные tg-значения — настоящий таджикский (best-effort, не native-reviewed — как принято в проекте); при сомнении реализатор уточняет, но НЕ копирует ru.

Затем регенерировать: `cd packages/i18n && bun run gen`.

- [ ] **Step 10: Дополнить presence-guard в `messages.test.ts`**

Добавить блок (рядом с существующими `includes the ... keys`):

```ts
it('includes the network section keys', () => {
  for (const key of [
    'op.shell.navGroup.network',
    'op.network.dest.branches', 'op.network.dest.branches.subtitle',
    'op.network.dest.billing', 'op.network.dest.billing.subtitle',
    'op.network.dest.install', 'op.network.dest.install.subtitle',
    'op.network.dest.journal', 'op.network.dest.journal.subtitle',
    'op.network.noAccess', 'op.network.placeholder'
  ] as const) {
    expect(messages.ru[key]).toBeTruthy();
    expect(messages.en[key]).toBeTruthy();
    expect(messages.tg[key]).toBeTruthy();
  }
});
```

- [ ] **Step 11: Дополнить `operatorVisibility.test.ts`**

Добавить блок:

```ts
describe('network workspace visibility', () => {
  it('opens network for an owner-permission session', () => {
    const session = { permissions: ['branches.view', 'billing.subscription.view', 'devices.install', 'audit.view'] } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'network')).toBe(true);
  });

  it('hides network for a cashier session', () => {
    const session = { permissions: rolePermissions.cashier_operator } as OperatorAuthSession;
    expect(canOpenWorkspace(session, 'network')).toBe(false);
  });
});
```

- [ ] **Step 12: Прогнать тесты + сборку**

Run: `bun test src/network/networkNav.test.ts src/operatorVisibility.test.ts && cd ../../packages/i18n && bun test src/messages.test.ts`
Expected: PASS.
Run (из `src/AFK4.Operator.App.Web`): `bun run build`
Expected: сборка зелёная (tsc + vite).

- [ ] **Step 13: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/network src/AFK4.Operator.App.Web/src/permissionNames.ts src/AFK4.Operator.App.Web/src/operatorTypes.ts src/AFK4.Operator.App.Web/src/operatorData.ts src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/src/WorkspaceRouter.tsx locales packages/i18n
git commit -m "feat(operator): каркас секции «Сеть» (nav + права + хост + плейсхолдеры)"
```

---

## Task 5: Frontend — экран Branches (свод по сети)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/api/clients/orgBranches.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/index.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/branches/branchRollupModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/branches/useBranchRollup.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/branches/RenameBranchModal.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/network/branches/BranchesDestination.tsx` (заменить плейсхолдер)
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/branches/branchRollupModel.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/branches/BranchesDestination.test.tsx`

**Interfaces:**
- Consumes:
  - `clients.orgBranches.getOwnerBranches(): Promise<OwnerBranchSummary[]>` (новый, ниже).
  - `clients.settings.getBranchProfile(branchId): Promise<{ name; city; ... }>` (существующий — как в `ClubDestination`).
  - `clients.dashboard.getSummary(branchId, range)` (существующий — вызывать ТОЧНО как `useShellData.ts`: `clients.dashboard.getSummary(branchId, dashboardRangeQuery(today, today))`; переиспользовать `dashboardRangeQuery`).
  - `createAuthenticatedOperatorClients(backend.config, backend.session)`.
  - `formatMinorUnits`/`<Money>` для денег; `useI18n().formatNumber`.
  - `useActiveBranch` select + навигация на Карту через проп/коллбэк (см. ниже — «Открыть»).
- Produces: `BranchesDestination` (реальный).

**Замечание про «Открыть»:** переключение активного филиала и переход на Карту делается тем же механизмом, что фундамент (`useActiveBranch` + `setWorkspace('map')`). В секции нет прямого доступа к `setWorkspace`. Реализация: `BranchesDestination` вызывает `backend`-независимый коллбэк из существующего слоя смены филиала — реализатор проверит, доступен ли `useActiveBranch().select(branchId)` внутри destination (он завязан на localStorage `afk4.operator.activeBranchId` и реактивен). Если переход на Карту требует `setWorkspace`, прокинуть опциональный `onOpenBranch?(branchId)` в проп destination от `NetworkWorkspace`←`WorkspaceRouter`←`App`; если это раздувает контракт — на первом шаге кнопка «Открыть» вызывает `useActiveBranch().select(branchId)` (карта переключится при следующем открытии Карты), а переход-на-Карту помечается TODO-фичей и выносится в follow-up (не плейсхолдер в коде — реальное частичное поведение с честной подписью). Реализатор выбирает минимальный честный вариант и фиксирует в отчёте.

- [ ] **Step 1: Создать клиент `orgBranches.ts`**

```ts
import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface OwnerBranchSummary {
  branchId: Guid;
  name: string;
}

export function createOrgBranchesClient(api: PlatformApiClient) {
  return {
    getOwnerBranches(): Promise<OwnerBranchSummary[]> {
      return api.get<OwnerBranchSummary[]>('/api/owner/branches');
    }
  };
}
```

Зарегистрировать в `src/AFK4.Operator.App.Web/src/api/clients/index.ts`:

```ts
import { createOrgBranchesClient } from './orgBranches';
// ... в createOperatorApiClients(api):
    orgBranches: createOrgBranchesClient(api),
```

- [ ] **Step 2: Написать падающий тест модели**

`src/AFK4.Operator.App.Web/src/network/branches/branchRollupModel.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { buildBranchRollup, type BranchRollupEntry } from './branchRollupModel';

const summary = {
  utilization: { onlineDevices: 3, offlineDevices: 1, activeSessions: 2 },
  revenue: { totalRevenue: { amountMinorUnits: 15000, currencyCode: 'TJS' } },
  alertPressure: { totalAlerts: 1 }
};

describe('buildBranchRollup', () => {
  it('aggregates KPIs across branches', () => {
    const entries: BranchRollupEntry[] = [
      { branchId: 'a', name: 'A', city: 'X', summary },
      { branchId: 'b', name: 'B', city: 'Y', summary }
    ];
    const vm = buildBranchRollup(entries);
    expect(vm.totals.branches).toBe(2);
    expect(vm.totals.devicesOnline).toEqual({ online: 6, total: 8 });
    expect(vm.totals.activeSessions).toBe(4);
    expect(vm.totals.revenue.amountMinorUnits).toBe(30000);
    expect(vm.totals.attention).toBe(2);
  });

  it('keeps a failed branch as a row with null kpis and excludes it from totals', () => {
    const entries: BranchRollupEntry[] = [
      { branchId: 'a', name: 'A', city: 'X', summary },
      { branchId: 'b', name: 'B', city: 'Y', summary: null }
    ];
    const vm = buildBranchRollup(entries);
    expect(vm.rows.find((r) => r.branchId === 'b')!.kpis).toBeNull();
    expect(vm.totals.branches).toBe(2);
    expect(vm.totals.activeSessions).toBe(2); // only branch A counted
  });
});
```

- [ ] **Step 3: Прогнать — падает**

Run: `bun test src/network/branches/branchRollupModel.test.ts`
Expected: FAIL — модуль не существует.

- [ ] **Step 4: Создать `branchRollupModel.ts`**

Операторский dashboard-summary loosely-typed (`Record<string, unknown>`), поэтому читаем поля защищённо. Поле суммы в operator-DTO — `amountMinorUnits` (minor units), не `{amount}`:

```ts
export interface BranchKpis {
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenue: { amountMinorUnits: number; currencyCode: string };
  attention: number;
}

export interface BranchRollupRow {
  branchId: string;
  name: string;
  city: string;
  kpis: BranchKpis | null; // null => this branch failed to load
}

export interface BranchRollupTotals {
  branches: number;
  devicesOnline: { online: number; total: number };
  activeSessions: number;
  revenue: { amountMinorUnits: number; currencyCode: string };
  attention: number;
}

export interface BranchRollupViewModel {
  rows: BranchRollupRow[];
  totals: BranchRollupTotals;
}

export interface BranchRollupEntry {
  branchId: string;
  name: string;
  city: string;
  summary: Record<string, unknown> | null;
}

function num(value: unknown): number {
  return typeof value === 'number' && Number.isFinite(value) ? value : 0;
}

function obj(value: unknown): Record<string, unknown> {
  return typeof value === 'object' && value !== null ? (value as Record<string, unknown>) : {};
}

function toKpis(summary: Record<string, unknown>): BranchKpis {
  const utilization = obj(summary.utilization);
  const revenue = obj(summary.revenue);
  const totalRevenue = obj(revenue.totalRevenue);
  const alertPressure = obj(summary.alertPressure);
  const online = num(utilization.onlineDevices);
  const offline = num(utilization.offlineDevices);
  return {
    devicesOnline: { online, total: online + offline },
    activeSessions: num(utilization.activeSessions),
    revenue: {
      amountMinorUnits: num(totalRevenue.amountMinorUnits),
      currencyCode: typeof totalRevenue.currencyCode === 'string' ? totalRevenue.currencyCode : ''
    },
    attention: num(alertPressure.totalAlerts)
  };
}

export function buildBranchRollup(entries: BranchRollupEntry[]): BranchRollupViewModel {
  const rows: BranchRollupRow[] = entries.map((e) => ({
    branchId: e.branchId,
    name: e.name,
    city: e.city,
    kpis: e.summary === null ? null : toKpis(e.summary)
  }));

  let online = 0, total = 0, activeSessions = 0, attention = 0, revenueAmount = 0, currencyCode = '';
  for (const row of rows) {
    if (row.kpis === null) continue;
    online += row.kpis.devicesOnline.online;
    total += row.kpis.devicesOnline.total;
    activeSessions += row.kpis.activeSessions;
    attention += row.kpis.attention;
    revenueAmount += row.kpis.revenue.amountMinorUnits;
    if (currencyCode === '' && row.kpis.revenue.currencyCode !== '') currencyCode = row.kpis.revenue.currencyCode;
  }

  return {
    rows,
    totals: {
      branches: rows.length,
      devicesOnline: { online, total },
      activeSessions,
      revenue: { amountMinorUnits: revenueAmount, currencyCode: currencyCode === '' ? 'TJS' : currencyCode },
      attention
    }
  };
}
```

Примечание: точное имя поля суммы (`amountMinorUnits` vs `amount`) в операторском ответе `dashboard/summary` сверить с реальным ответом бэка (`OperatorDashboardSummaryDto` — Money как minor units). Если сервер отдаёт вложенный `{ amount, currencyCode }` в major — привести к minor через `majorToMinor`. Тест использует `amountMinorUnits`; при расхождении поправить и тест, и модель согласованно.

- [ ] **Step 5: Прогнать — зелёный**

Run: `bun test src/network/branches/branchRollupModel.test.ts`
Expected: PASS.

- [ ] **Step 6: Создать `useBranchRollup.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import { buildBranchRollup, type BranchRollupEntry, type BranchRollupViewModel } from './branchRollupModel';

export interface RollupClient {
  getOwnerBranches(): Promise<{ branchId: string; name: string }[]>;
  getBranchProfile(branchId: string): Promise<{ name?: string; city?: string }>;
  getBranchSummary(branchId: string): Promise<Record<string, unknown>>;
}

export type BranchRollupState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; data: BranchRollupViewModel; retry: () => void };

export function useBranchRollup(client: RollupClient, unnamedLabel: string): BranchRollupState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [data, setData] = useState<BranchRollupViewModel | null>(null);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    const c = clientRef.current;
    (async () => {
      const branches = await c.getOwnerBranches();
      const entries = await Promise.all(branches.map(async (b): Promise<BranchRollupEntry> => {
        const [profile, summary] = await Promise.all([
          c.getBranchProfile(b.branchId).catch(() => null),
          c.getBranchSummary(b.branchId).catch(() => null)
        ]);
        return {
          branchId: b.branchId,
          name: profile?.name ?? b.name ?? unnamedLabel,
          city: profile?.city ?? '',
          summary
        };
      }));
      if (!cancelled) { setData(buildBranchRollup(entries)); setPhase('ready'); }
    })().catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [tick, unnamedLabel]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || data === null) return { status: 'loading', retry };
  return { status: 'ready', data, retry };
}
```

- [ ] **Step 7: Создать `RenameBranchModal.tsx`** (эталон — `PanelModal` + `.mgmt-form`)

```tsx
import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../../PanelModal';

export function RenameBranchModal({ branchId, organizationId, initialName, initialCity, onClose, onSave }: {
  branchId: string;
  organizationId: string;
  initialName: string;
  initialCity: string;
  onClose: () => void;
  onSave: (request: { organizationId: string; name: string; city: string }) => Promise<void>;
}): JSX.Element {
  const { t } = useI18n();
  const [name, setName] = useState(initialName);
  const [city, setCity] = useState(initialCity);
  const [busy, setBusy] = useState(false);
  const valid = name.trim() !== '' && city.trim() !== '';

  async function submit() {
    setBusy(true);
    try {
      await onSave({ organizationId, name: name.trim(), city: city.trim() });
      onClose();
    } finally {
      setBusy(false);
    }
  }

  return (
    <PanelModal title={t('op.network.branches.rename.title')} onClose={onClose} closeDisabled={busy}>
      <form className="mgmt-form" onSubmit={(e) => { e.preventDefault(); if (valid) void submit(); }}>
        <div className="mgmt-form-grid">
          <label>{t('op.network.branches.field.name')}
            <input value={name} disabled={busy} autoFocus onChange={(e) => setName(e.currentTarget.value)} />
          </label>
          <label>{t('op.network.branches.field.city')}
            <input value={city} disabled={busy} onChange={(e) => setCity(e.currentTarget.value)} />
          </label>
        </div>
        <div className="mgmt-form-actions">
          <button type="button" className="ui-btn" onClick={onClose} disabled={busy}>{t('common.cancel')}</button>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || !valid}>{t('common.save')}</button>
        </div>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 8: Написать падающий тест экрана**

`src/AFK4.Operator.App.Web/src/network/branches/BranchesDestination.test.tsx`:

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

const summary = {
  utilization: { onlineDevices: 2, offlineDevices: 0, activeSessions: 1 },
  revenue: { totalRevenue: { amountMinorUnits: 5000, currencyCode: 'TJS' } },
  alertPressure: { totalAlerts: 0 }
};

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBranches: { getOwnerBranches: mock(async () => [{ branchId: 'b1', name: 'Центр' }]) },
    settings: { getBranchProfile: mock(async () => ({ name: 'Центр', city: 'Душанбе' })) },
    dashboard: { getSummary: mock(async () => summary) }
  })
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('BranchesDestination', () => {
  it('renders branch cards with the branch name', async () => {
    const { BranchesDestination } = await import('./BranchesDestination');
    render(
      <I18nProvider initialLocale="ru">
        <BranchesDestination backend={backend as never} />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Душанбе')).toBeInTheDocument());
    expect(screen.getByText('Центр')).toBeInTheDocument();
  });
});
```

Примечание: имя мок-метода (`dashboard.getSummary`) сверить с реальным клиентом `dashboard.ts`. Если у оператора метод называется иначе — подставить фактическое.

- [ ] **Step 9: Прогнать — падает**

Run: `bun test src/network/branches/BranchesDestination.test.tsx`
Expected: FAIL — `BranchesDestination` пока плейсхолдер.

- [ ] **Step 10: Реализовать `BranchesDestination.tsx`**

```tsx
import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState, Money } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import { dashboardRangeQuery } from '../../useShellData'; // reuse the exact call shape used by the shell KPI loader
import type { OperatorBackendContext } from '../../management/destinations/types';
import { useBranchRollup, type RollupClient } from './useBranchRollup';
import { RenameBranchModal } from './RenameBranchModal';

interface RenameTarget { branchId: string; name: string; city: string; }

export function BranchesDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatNumber } = useI18n();

  const client = useMemo<RollupClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const today = new Date();
    return {
      getOwnerBranches: () => clients.orgBranches.getOwnerBranches(),
      getBranchProfile: (id) => clients.settings.getBranchProfile(id),
      getBranchSummary: (id) => clients.dashboard.getSummary(id, dashboardRangeQuery(today, today))
    };
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBranchRollup(
    client ?? { getOwnerBranches: async () => [], getBranchProfile: async () => ({}), getBranchSummary: async () => ({}) },
    t('op.network.branches.unnamed')
  );
  const [renameTarget, setRenameTarget] = useState<RenameTarget | null>(null);

  const screenState = backend === null ? 'loading' : state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.branches')}
      subtitle={t('op.network.dest.branches.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          <div className="network-branches-totals">
            <Totals label={t('op.network.branches.totals.branches')} value={formatNumber(state.data.totals.branches)} />
            <Totals label={t('op.network.branches.kpi.devices')} value={`${formatNumber(state.data.totals.devicesOnline.online)} / ${formatNumber(state.data.totals.devicesOnline.total)}`} />
            <Totals label={t('op.network.branches.kpi.sessions')} value={formatNumber(state.data.totals.activeSessions)} />
            <Totals label={t('op.network.branches.kpi.revenue')} value={<Money minorUnits={state.data.totals.revenue.amountMinorUnits} currencyCode={state.data.totals.revenue.currencyCode} />} />
            <Totals label={t('op.network.branches.kpi.attention')} value={formatNumber(state.data.totals.attention)} />
          </div>

          {state.data.rows.length === 0 ? (
            <EmptyState title={t('op.network.branches.empty')} />
          ) : (
            <div className="network-branches-grid">
              {state.data.rows.map((row) => (
                <section key={row.branchId} className="management-panel network-branch-card">
                  <header>
                    <h3>{row.name}</h3>
                    <span className="network-branch-city">{row.city}</span>
                  </header>
                  {row.kpis === null ? (
                    <p className="network-branch-error">{t('op.network.branches.card.error')}</p>
                  ) : (
                    <dl className="network-branch-kpis">
                      <Stat label={t('op.network.branches.kpi.devices')} value={`${formatNumber(row.kpis.devicesOnline.online)} / ${formatNumber(row.kpis.devicesOnline.total)}`} />
                      <Stat label={t('op.network.branches.kpi.sessions')} value={formatNumber(row.kpis.activeSessions)} />
                      <Stat label={t('op.network.branches.kpi.revenue')} value={<Money minorUnits={row.kpis.revenue.amountMinorUnits} currencyCode={row.kpis.revenue.currencyCode} />} />
                      <Stat label={t('op.network.branches.kpi.attention')} value={formatNumber(row.kpis.attention)} />
                    </dl>
                  )}
                  <div className="network-branch-actions">
                    <button type="button" className="ui-btn" onClick={() => setRenameTarget({ branchId: row.branchId, name: row.name, city: row.city })}>
                      {t('op.network.branches.rename')}
                    </button>
                  </div>
                </section>
              ))}
            </div>
          )}

          <div className="network-branches-add">
            <button type="button" className="ui-btn" disabled>{t('op.network.branches.add')}</button>
            <p className="network-branches-add-note">{t('op.network.branches.add.unavailable')}</p>
          </div>

          {renameTarget !== null && backend !== null && (
            <RenameBranchModal
              branchId={renameTarget.branchId}
              organizationId={backend.session.organizationId}
              initialName={renameTarget.name}
              initialCity={renameTarget.city}
              onClose={() => setRenameTarget(null)}
              onSave={async (request) => {
                const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
                await clients.settings.updateBranchProfile(renameTarget.branchId, request);
                state.retry();
              }}
            />
          )}
        </>
      )}
    </ManagementScreen>
  );
}

function Totals({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="management-panel network-total">
      <span className="network-total-label">{label}</span>
      <span className="network-total-value">{value}</span>
    </div>
  );
}

function Stat({ label, value }: { label: string; value: React.ReactNode }) {
  return (
    <div className="network-stat">
      <dt>{label}</dt>
      <dd>{value}</dd>
    </div>
  );
}
```

Примечания:
- `dashboardRangeQuery` — если он не экспортируется из `useShellData.ts`, найти где он определён (там же, где формируется запрос `dashboard/summary`) и импортировать оттуда; при отсутствии экспортируемого хелпера — вынести его в отдельный util (`operatorHelpers.ts`) и импортировать в оба места (DRY), НЕ дублировать формулу диапазона.
- «Открыть» филиал не включён в этот шаг (см. «Замечание про Открыть» выше) — только «Переименовать». Если реализатор проведёт `useActiveBranch().select` без раздувания контракта — добавить кнопку «Открыть»; иначе зафиксировать follow-up в отчёте.
- CSS-классы `network-*` — добавить минимальные стили в новый `src/AFK4.Operator.App.Web/src/styles/24-network.css` (импортировать в общий styles-агрегатор) ИЛИ переиспользовать существующие `.management-panel`/грид-утилиты. Стили — не блокер функциональности; сетка карточек через `.management-panel` + простой grid.
- `updateBranchProfile` — существующий метод `clients.settings.updateBranchProfile(branchId, { organizationId, name, city })` (как в `ClubDestination` save-path).

- [ ] **Step 11: Добавить i18n-ключи Branches** (ru/en/tg)

```
op.network.branches.unnamed        "Без названия" / "Unnamed" / "Беном"
op.network.branches.totals.branches "Филиалов" / "Branches" / "Филиалҳо"
op.network.branches.kpi.devices    "ПК онлайн" / "PCs online" / "ПК онлайн"
op.network.branches.kpi.sessions   "Сессий" / "Sessions" / "Сессияҳо"
op.network.branches.kpi.revenue    "Выручка сегодня" / "Revenue today" / "Даромади имрӯз"
op.network.branches.kpi.attention  "Требуют внимания" / "Need attention" / "Диққат металабанд"
op.network.branches.card.error     "Не удалось загрузить филиал" / "Failed to load branch" / "Боркунии филиал ноком шуд"
op.network.branches.empty          "Филиалы не найдены" / "No branches" / "Филиал ёфт нашуд"
op.network.branches.rename         "Переименовать" / "Rename" / "Тағйири ном"
op.network.branches.rename.title   "Переименовать филиал" / "Rename branch" / "Тағйири номи филиал"
op.network.branches.field.name     "Название" / "Name" / "Ном"
op.network.branches.field.city     "Город" / "City" / "Шаҳр"
op.network.branches.add            "Добавить филиал" / "Add branch" / "Илова кардани филиал"
op.network.branches.add.unavailable "Создание филиала пока недоступно" / "Adding a branch isn't available yet" / "Илова кардани филиал ҳанӯз дастрас нест"
```

(tg: «ПК» и «онлайн» — заимствования; если `op.network.branches.kpi.devices` совпадёт с ru — в allowlist с обоснованием. Регенерировать `bun run gen`.) Дополнить presence-блок в `messages.test.ts` этими ключами.

- [ ] **Step 12: Прогнать тесты + сборку**

Run: `bun test src/network/branches/ && cd ../../packages/i18n && bun test src/messages.test.ts`
Expected: PASS.
Run (из operator web): `bun run build`
Expected: зелёная.

- [ ] **Step 13: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/network/branches src/AFK4.Operator.App.Web/src/api/clients locales packages/i18n src/AFK4.Operator.App.Web/src/styles
git commit -m "feat(operator): экран «Сеть → Филиалы» (свод по сети)"
```

---

## Task 6: Frontend — экран Billing (подписка, read-only)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/api/clients/orgBilling.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/index.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/billing/billingModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/billing/useBilling.ts`
- Modify: `src/AFK4.Operator.App.Web/src/network/billing/BillingDestination.tsx`
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/billing/billingModel.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/billing/BillingDestination.test.tsx`

**Interfaces:**
- Consumes: `clients.orgBilling.getSubscription(orgId)`, `clients.orgBilling.listInvoices(orgId)`; `<Money>`; `useI18n().formatDate`.
- Produces: `BillingDestination` (реальный).

DTO бэка (camelCase в JSON): `TenantSubscription { planCode, status, currentPeriodStartUtc, currentPeriodEndUtc, nextInvoiceUtc|null, amountMinorUnits, currencyCode, cancelAtPeriodEnd }`; `Invoice { invoiceId, number, issuedAtUtc, dueAtUtc, amountMinorUnits, currencyCode, status }`.

- [ ] **Step 1: Клиент `orgBilling.ts`**

```ts
import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';

export interface TenantSubscriptionDto {
  planCode: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  amountMinorUnits: number;
  currencyCode: string;
  cancelAtPeriodEnd: boolean;
}

export interface InvoiceDto {
  invoiceId: string;
  number: number;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

export function createOrgBillingClient(api: PlatformApiClient) {
  return {
    getSubscription(organizationId: Guid): Promise<TenantSubscriptionDto> {
      return api.get<TenantSubscriptionDto>(`/api/organizations/${organizationId}/subscription`);
    },
    listInvoices(organizationId: Guid): Promise<InvoiceDto[]> {
      return api.get<InvoiceDto[]>(`/api/organizations/${organizationId}/invoices`);
    }
  };
}
```

Зарегистрировать в `api/clients/index.ts`: `orgBilling: createOrgBillingClient(api)`.

- [ ] **Step 2: Падающий тест модели**

`billingModel.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { subscriptionStatusLabelKey, invoiceStatusLabelKey, subscriptionStatusTone } from './billingModel';

describe('billingModel', () => {
  it('maps known subscription statuses to label keys', () => {
    expect(subscriptionStatusLabelKey('active')).toBe('op.network.billing.subStatus.active');
    expect(subscriptionStatusLabelKey('unknown')).toBeNull();
  });
  it('maps invoice statuses to label keys', () => {
    expect(invoiceStatusLabelKey('paid')).toBe('op.network.billing.invStatus.paid');
  });
  it('maps status to a chip tone', () => {
    expect(subscriptionStatusTone('active')).toBe('ok');
    expect(subscriptionStatusTone('past_due')).toBe('warning');
  });
});
```

- [ ] **Step 3: Прогнать — падает** — `bun test src/network/billing/billingModel.test.ts` → FAIL.

- [ ] **Step 4: `billingModel.ts`**

Тона чипа — под операторский `.ui-chip` (значения `ok`/`warning`/`muted`/`danger` — сверить фактические модификаторы `.ui-chip--*` в `02-ui-kit.css`; ниже условные, реализатор приводит к реальным):

```ts
import type { MessageKey } from '@afk4/i18n';

export type ChipTone = 'ok' | 'warning' | 'muted' | 'danger';

const SUB_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'op.network.billing.subStatus.trial',
  active: 'op.network.billing.subStatus.active',
  past_due: 'op.network.billing.subStatus.pastDue',
  cancelled: 'op.network.billing.subStatus.cancelled'
};

const SUB_STATUS_TONE: Record<string, ChipTone> = {
  trial: 'muted', active: 'ok', past_due: 'warning', cancelled: 'muted'
};

const INV_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'op.network.billing.invStatus.issued',
  paid: 'op.network.billing.invStatus.paid',
  void: 'op.network.billing.invStatus.void',
  overdue: 'op.network.billing.invStatus.overdue'
};

const INV_STATUS_TONE: Record<string, ChipTone> = {
  issued: 'muted', paid: 'ok', void: 'muted', overdue: 'danger'
};

export function subscriptionStatusLabelKey(status: string): MessageKey | null { return SUB_STATUS_LABEL[status] ?? null; }
export function subscriptionStatusTone(status: string): ChipTone { return SUB_STATUS_TONE[status] ?? 'muted'; }
export function invoiceStatusLabelKey(status: string): MessageKey | null { return INV_STATUS_LABEL[status] ?? null; }
export function invoiceStatusTone(status: string): ChipTone { return INV_STATUS_TONE[status] ?? 'muted'; }
```

- [ ] **Step 5: Прогнать — зелёный** — `bun test src/network/billing/billingModel.test.ts` → PASS.

- [ ] **Step 6: `useBilling.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { InvoiceDto, TenantSubscriptionDto } from '../../api/clients/orgBilling';

export interface BillingClient {
  getSubscription(organizationId: string): Promise<TenantSubscriptionDto>;
  listInvoices(organizationId: string): Promise<InvoiceDto[]>;
}

export type BillingState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; subscription: TenantSubscriptionDto; invoices: InvoiceDto[]; retry: () => void };

export function useBilling(client: BillingClient, organizationId: string): BillingState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [subscription, setSubscription] = useState<TenantSubscriptionDto | null>(null);
  const [invoices, setInvoices] = useState<InvoiceDto[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([clientRef.current.getSubscription(organizationId), clientRef.current.listInvoices(organizationId)])
      .then(([sub, inv]) => { if (!cancelled) { setSubscription(sub); setInvoices(inv); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
  }, [organizationId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || subscription === null) return { status: 'loading' };
  return { status: 'ready', subscription, invoices, retry };
}
```

- [ ] **Step 7: Падающий тест экрана**

`BillingDestination.test.tsx`:

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBilling: {
      getSubscription: mock(async () => ({
        planCode: 'PRO', status: 'active', currentPeriodStartUtc: '2026-07-01T00:00:00Z',
        currentPeriodEndUtc: '2026-07-31T00:00:00Z', nextInvoiceUtc: '2026-08-01T00:00:00Z',
        amountMinorUnits: 120000, currencyCode: 'TJS', cancelAtPeriodEnd: false
      })),
      listInvoices: mock(async () => [{ invoiceId: 'i1', number: 42, issuedAtUtc: '2026-07-01T00:00:00Z', dueAtUtc: '2026-07-10T00:00:00Z', amountMinorUnits: 120000, currencyCode: 'TJS', status: 'paid' }])
    }
  })
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('BillingDestination', () => {
  it('renders plan code and an invoice row', async () => {
    const { BillingDestination } = await import('./BillingDestination');
    render(<I18nProvider initialLocale="ru"><BillingDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('PRO')).toBeInTheDocument());
    expect(screen.getByText('42')).toBeInTheDocument();
  });
});
```

- [ ] **Step 8: Прогнать — падает** — FAIL (плейсхолдер).

- [ ] **Step 9: Реализовать `BillingDestination.tsx`**

```tsx
import { useMemo } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { Money } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../management/destinations/types';
import { useBilling, type BillingClient } from './useBilling';
import { subscriptionStatusLabelKey, subscriptionStatusTone, invoiceStatusLabelKey, invoiceStatusTone } from './billingModel';

export function BillingDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();

  const client = useMemo<BillingClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return { getSubscription: (id) => clients.orgBilling.getSubscription(id), listInvoices: (id) => clients.orgBilling.listInvoices(id) };
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useBilling(
    client ?? { getSubscription: async () => { throw new Error('no backend'); }, listInvoices: async () => [] },
    backend?.session.organizationId ?? ''
  );

  const screenState = backend === null ? 'loading' : state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.billing')}
      subtitle={t('op.network.dest.billing.subtitle')}
      contentWidth="wide"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          <section className="management-panel network-billing-sub">
            <h3>{t('op.network.billing.subscription')}</h3>
            <dl className="network-billing-grid">
              <Field label={t('op.network.billing.plan')} value={state.subscription.planCode} />
              <Field label={t('op.network.billing.status')} value={
                <span className={`ui-chip ui-chip--${subscriptionStatusTone(state.subscription.status)}`}>
                  {subscriptionStatusLabelKey(state.subscription.status) ? t(subscriptionStatusLabelKey(state.subscription.status)!) : state.subscription.status}
                </span>
              } />
              <Field label={t('op.network.billing.amount')} value={<Money minorUnits={state.subscription.amountMinorUnits} currencyCode={state.subscription.currencyCode} />} />
              <Field label={t('op.network.billing.period')} value={`${formatDate(state.subscription.currentPeriodStartUtc)} — ${formatDate(state.subscription.currentPeriodEndUtc)}`} />
              <Field label={t('op.network.billing.nextInvoice')} value={state.subscription.nextInvoiceUtc ? formatDate(state.subscription.nextInvoiceUtc) : '—'} />
            </dl>
          </section>

          <section className="management-panel network-billing-invoices">
            <h3>{t('op.network.billing.invoices')}</h3>
            {state.invoices.length === 0 ? (
              <p className="network-billing-empty">{t('op.network.billing.invoices.empty')}</p>
            ) : (
              <div className="table-panel">
                <div className="ctable-head" style={{ gridTemplateColumns: '0.6fr 1fr 1fr 1fr 0.8fr' }} aria-hidden="true">
                  <span>{t('op.network.billing.col.number')}</span>
                  <span>{t('op.network.billing.col.issued')}</span>
                  <span>{t('op.network.billing.col.due')}</span>
                  <span>{t('op.network.billing.col.amount')}</span>
                  <span>{t('op.network.billing.col.status')}</span>
                </div>
                <div className="ctable-body">
                  {state.invoices.map((inv) => (
                    <div key={inv.invoiceId} className="ctable-row" style={{ gridTemplateColumns: '0.6fr 1fr 1fr 1fr 0.8fr' }}>
                      <span>{inv.number}</span>
                      <span>{formatDate(inv.issuedAtUtc)}</span>
                      <span>{formatDate(inv.dueAtUtc)}</span>
                      <span><Money minorUnits={inv.amountMinorUnits} currencyCode={inv.currencyCode} /></span>
                      <span className={`ui-chip ui-chip--${invoiceStatusTone(inv.status)}`}>
                        {invoiceStatusLabelKey(inv.status) ? t(invoiceStatusLabelKey(inv.status)!) : inv.status}
                      </span>
                    </div>
                  ))}
                </div>
              </div>
            )}
          </section>
        </>
      )}
    </ManagementScreen>
  );
}

function Field({ label, value }: { label: string; value: React.ReactNode }) {
  return <div className="network-field"><dt>{label}</dt><dd>{value}</dd></div>;
}
```

Примечание: классы таблицы `.table-panel`/`.ctable-head`/`.ctable-row`/`.ctable-body` — существующие (раздел «Клиенты», `12-players.css`); `.ui-chip--*` модификаторы сверить с `02-ui-kit.css` и привести тона к фактическим. Для read-only таблицы `MgmtTable` избыточен (в нём toolbar/rowActions/selection) — простой grid по существующим `.ctable-*` классам корректнее.

- [ ] **Step 10: i18n-ключи Billing** (ru/en/tg)

```
op.network.billing.subscription   "Подписка" / "Subscription" / "Обунашавӣ"
op.network.billing.plan           "Тариф" / "Plan" / "Таъриф"
op.network.billing.status         "Статус" / "Status" / "Ҳолат"
op.network.billing.amount         "Сумма" / "Amount" / "Маблағ"
op.network.billing.period         "Период" / "Period" / "Давра"
op.network.billing.nextInvoice    "Следующий счёт" / "Next invoice" / "Ҳисоби навбатӣ"
op.network.billing.invoices       "Счета" / "Invoices" / "Ҳисобҳо"
op.network.billing.invoices.empty "Счетов нет" / "No invoices" / "Ҳисобҳо нестанд"
op.network.billing.col.number     "№" / "No." / "№"
op.network.billing.col.issued     "Выставлен" / "Issued" / "Содиршуда"
op.network.billing.col.due        "Срок" / "Due" / "Мӯҳлат"
op.network.billing.col.amount     "Сумма" / "Amount" / "Маблағ"
op.network.billing.col.status     "Статус" / "Status" / "Ҳолат"
op.network.billing.subStatus.trial "Пробный" / "Trial" / "Озмоишӣ"
op.network.billing.subStatus.active "Активна" / "Active" / "Фаъол"
op.network.billing.subStatus.pastDue "Просрочена" / "Past due" / "Гузашта"
op.network.billing.subStatus.cancelled "Отменена" / "Cancelled" / "Бекоршуда"
op.network.billing.invStatus.issued "Выставлен" / "Issued" / "Содиршуда"
op.network.billing.invStatus.paid "Оплачен" / "Paid" / "Пардохтшуда"
op.network.billing.invStatus.void "Аннулирован" / "Void" / "Ботил"
op.network.billing.invStatus.overdue "Просрочен" / "Overdue" / "Гузашта"
```

(«№» одинаков в ru/tg — в allowlist. Регенерировать + presence-блок.)

- [ ] **Step 11: Прогнать тесты + сборку** — `bun test src/network/billing/ && (i18n) && bun run build` → PASS/зелёная.

- [ ] **Step 12: Commit**

```bash
git commit -am "feat(operator): экран «Сеть → Подписка» (read-only billing)"
```

---

## Task 7: Frontend — экран Install (установщик-Мастер)

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorConfig.ts` (optional `setupInstallerUrl`)
- Modify: `src/AFK4.Operator.App.Web/src/devHostBridge.ts` (dev-инжект, по надобности)
- Create: `src/AFK4.Operator.App.Web/src/network/install/installModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/network/install/InstallDestination.tsx`
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/install/installModel.test.ts`
- Test: `src/AFK4.Operator.App.Web/src/network/install/InstallDestination.test.tsx`

**Interfaces:**
- Consumes: `getOperatorConfig()` (через `backend.config.setupInstallerUrl`); `clients.orgBranches.getOwnerBranches()` (список филиалов, informational).
- Produces: `InstallDestination` (реальный). Никакой ручной генерации кода.

- [ ] **Step 1: Добавить `setupInstallerUrl` в конфиг**

В `operatorConfig.ts` — в `OperatorConfig` + fallback:

```ts
export interface OperatorConfig {
  runtime: string;
  shellMode: string;
  platformBaseUrl: string;
  currencyCode: string;
  appVersion?: string;
  organizationId?: string;
  branchId?: string;
  setupInstallerUrl?: string; // configured at release; empty => show "obtain from IT" (no broken link)
}
```

В `fallbackConfig` не задаём (остаётся `undefined` в dev — экран покажет честный текст). В `browserConfigFromEnv()` — необязательный env:

```ts
  return {
    runtime: 'browser',
    shellMode: 'web',
    platformBaseUrl,
    currencyCode: 'TJS',
    setupInstallerUrl: import.meta.env.VITE_SETUP_INSTALLER_URL || undefined
  };
```

(WPF-хост при желании инжектит `setupInstallerUrl` в `window.__AFK4_OPERATOR_CONFIG__` — правка .NET-стороны вне этого фронт-таска; поле optional, поэтому обратно совместимо.)

- [ ] **Step 2: Падающий тест модели**

`installModel.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { getInstallerUrl } from './installModel';

describe('getInstallerUrl', () => {
  it('returns the configured url when present', () => {
    expect(getInstallerUrl({ setupInstallerUrl: 'https://dl.example/afk4-client.exe' } as never)).toBe('https://dl.example/afk4-client.exe');
  });
  it('returns null when unset (no broken fallback link)', () => {
    expect(getInstallerUrl({} as never)).toBeNull();
    expect(getInstallerUrl({ setupInstallerUrl: '   ' } as never)).toBeNull();
  });
});
```

- [ ] **Step 3: Прогнать — падает** — FAIL.

- [ ] **Step 4: `installModel.ts`**

```ts
import type { OperatorConfig } from '../../operatorConfig';

export function getInstallerUrl(config: Pick<OperatorConfig, 'setupInstallerUrl'>): string | null {
  const url = config.setupInstallerUrl;
  return typeof url === 'string' && url.trim().length > 0 ? url.trim() : null;
}
```

- [ ] **Step 5: Прогнать — зелёный** — PASS.

- [ ] **Step 6: Падающий тест экрана**

`InstallDestination.test.tsx`:

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBranches: { getOwnerBranches: mock(async () => [{ branchId: 'b1', name: 'Центр' }]) }
  })
}));

function backend(setupInstallerUrl?: string) {
  return { config: { platformBaseUrl: 'x', currencyCode: 'TJS', setupInstallerUrl }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };
}

describe('InstallDestination', () => {
  it('enables download when url is configured', async () => {
    const { InstallDestination } = await import('./InstallDestination');
    render(<I18nProvider initialLocale="ru"><InstallDestination backend={backend('https://dl/afk4.exe') as never} /></I18nProvider>);
    const link = await screen.findByRole('link', { name: /скач/i });
    expect(link).toHaveAttribute('href', 'https://dl/afk4.exe');
  });

  it('shows an honest "obtain from IT" note when url is missing', async () => {
    const { InstallDestination } = await import('./InstallDestination');
    render(<I18nProvider initialLocale="ru"><InstallDestination backend={backend(undefined) as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText(/IT|релиз/i)).toBeInTheDocument());
    expect(screen.queryByRole('link', { name: /скач/i })).toBeNull();
  });
});
```

- [ ] **Step 7: Прогнать — падает** — FAIL.

- [ ] **Step 8: Реализовать `InstallDestination.tsx`**

```tsx
import { useEffect, useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../management/destinations/types';
import { getInstallerUrl } from './installModel';

export function InstallDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  const installerUrl = backend === null ? null : getInstallerUrl(backend.config);
  const [branches, setBranches] = useState<{ branchId: string; name: string }[]>([]);

  const clients = useMemo(
    () => (backend === null ? null : createAuthenticatedOperatorClients(backend.config, backend.session)),
    [backend?.config.platformBaseUrl, backend?.session.accessToken]
  );

  useEffect(() => {
    if (clients === null) return undefined;
    let active = true;
    clients.orgBranches.getOwnerBranches().then((list) => { if (active) setBranches(list); }).catch(() => { /* informational list; ignore */ });
    return () => { active = false; };
  }, [clients]);

  const steps = [
    t('op.network.install.step.run'),
    t('op.network.install.step.signIn'),
    t('op.network.install.step.branch'),
    t('op.network.install.step.role'),
    t('op.network.install.step.name'),
    t('op.network.install.step.done')
  ];

  return (
    <ManagementScreen title={t('op.network.dest.install')} subtitle={t('op.network.dest.install.subtitle')} contentWidth="wide">
      <section className="management-panel network-install-get">
        <h3>{t('op.network.install.get.title')}</h3>
        <p>{t('op.network.install.get.lead')}</p>
        {installerUrl !== null ? (
          <a className="ui-btn ui-btn--primary" href={installerUrl} download>{t('op.network.install.download')}</a>
        ) : (
          <p className="network-install-nolink">{t('op.network.install.noUrl')}</p>
        )}
      </section>

      <section className="management-panel network-install-steps">
        <h3>{t('op.network.install.steps.title')}</h3>
        <ol className="network-install-step-list">
          {steps.map((step, i) => <li key={i}>{step}</li>)}
        </ol>
      </section>

      <section className="management-panel network-install-branches">
        <h3>{t('op.network.install.branches.title')}</h3>
        {branches.length === 0 ? (
          <p className="network-install-branches-empty">{t('op.network.install.branches.empty')}</p>
        ) : (
          <ul className="network-install-branch-list">
            {branches.map((b) => <li key={b.branchId}>{b.name}</li>)}
          </ul>
        )}
      </section>
    </ManagementScreen>
  );
}
```

- [ ] **Step 9: Прогнать — зелёный** — PASS.

- [ ] **Step 10: i18n-ключи Install** (ru/en/tg)

```
op.network.install.get.title      "Установщик" / "Installer" / "Насбкунанда"
op.network.install.get.lead       "Скачайте установщик и запустите его на новом ПК. Мастер настройки сам проведёт подключение." / "Download the installer and run it on the new PC. The setup wizard walks through onboarding." / "Насбкунандаро бор кунед ва дар ПК-и нав оғоз кунед. Устоди танзим пайвастро худаш мегузаронад."
op.network.install.download       "Скачать установщик" / "Download installer" / "Боркунии насбкунанда"
op.network.install.noUrl          "Ссылка на установщик не настроена — получите его у IT или из релиза." / "The installer link isn't configured — obtain it from IT or the release." / "Пайванди насбкунанда танзим нашудааст — онро аз IT ё аз релиз гиред."
op.network.install.steps.title    "Как установить" / "How to install" / "Тарзи насб"
op.network.install.step.run       "Запустите установщик на новом ПК" / "Run the installer on the new PC" / "Насбкунандаро дар ПК-и нав оғоз кунед"
op.network.install.step.signIn    "Войдите по телефону или логину" / "Sign in with phone or login" / "Бо телефон ё логин ворид шавед"
op.network.install.step.branch    "Выберите филиал" / "Choose the branch" / "Филиалро интихоб кунед"
op.network.install.step.role      "Выберите роль ПК: игровой или рабочее место" / "Choose the PC role: gaming or workstation" / "Нақши ПК-ро интихоб кунед: бозӣ ё ҷойи корӣ"
op.network.install.step.name      "Задайте имя ПК" / "Set the PC name" / "Номи ПК-ро таъин кунед"
op.network.install.step.done      "Готово — Мастер завершит установку" / "Done — the wizard finishes setup" / "Тайёр — устод насбро анҷом медиҳад"
op.network.install.branches.title "Филиалы" / "Branches" / "Филиалҳо"
op.network.install.branches.empty "Филиалы не найдены" / "No branches" / "Филиал ёфт нашуд"
```

(Регенерировать + presence-блок.)

- [ ] **Step 11: Прогнать тесты + сборку** — `bun test src/network/install/ && (i18n) && bun run build`.

- [ ] **Step 12: Commit** — `git commit -am "feat(operator): экран «Сеть → Установка» (установщик-Мастер)"`.

---

## Task 8: Frontend — экран Journal (org-level аудит)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/api/clients/orgAudit.ts`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/index.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/journal/dateRange.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/journal/orgAuditModel.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/journal/useOrgAudit.ts`
- Create: `src/AFK4.Operator.App.Web/src/network/journal/OrgAuditFilters.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/network/journal/JournalDestination.tsx`
- Modify: `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.test.ts`
- Tests: `dateRange.test.ts`, `orgAuditModel.test.ts`, `JournalDestination.test.tsx`

**Interfaces:**
- Consumes: `clients.orgAudit.searchOrganizationAudit(organizationId, query)`; `useI18n().formatDate`.
- Produces: `JournalDestination` (реальный).

DTO ответа: `{ records: AuditRecordDto[]; limit: number }`, `AuditRecordDto { auditRecordId, organizationId, branchId|null, actorStaffUserId|null, action, targetType, targetId|null, outcome, sourceApp, detailsJson, createdAtUtc, actorPlatformAdminUserId|null }`.

- [ ] **Step 1: Клиент `orgAudit.ts`**

```ts
import { PlatformApiClient } from '../../platformApi';
import type { Guid } from '../types';
import { normalizeReportQuery } from '../queryHelpers';

export interface OrgAuditRecordDto {
  auditRecordId: string;
  branchId: string | null;
  actorStaffUserId: string | null;
  actorPlatformAdminUserId: string | null;
  action: string;
  targetType: string;
  targetId: string | null;
  outcome: string;
  sourceApp: string;
  detailsJson: string;
  createdAtUtc: string;
}

export interface OrgAuditSearchResultDto { records: OrgAuditRecordDto[]; limit: number; }

export interface OrgAuditQuery {
  action?: string | null;
  outcome?: string | null;
  targetType?: string | null;
  fromUtc?: string | null;
  toUtc?: string | null;
  limit?: number | null;
}

export function createOrgAuditClient(api: PlatformApiClient) {
  return {
    searchOrganizationAudit(organizationId: Guid, query: OrgAuditQuery): Promise<OrgAuditSearchResultDto> {
      return api.get<OrgAuditSearchResultDto>(`/api/organizations/${organizationId}/audit`, normalizeReportQuery(query));
    }
  };
}
```

(`normalizeReportQuery` — тот же хелпер, что использует operator `audit.ts`; сверить, что он отбрасывает null/undefined и сериализует даты.) Зарегистрировать в `index.ts`: `orgAudit: createOrgAuditClient(api)`.

- [ ] **Step 2: `dateRange.ts` + падающий тест**

Перенос из Platform.Web `reportsModel` (verbatim helpers). `dateRange.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { presetRange } from './dateRange';

describe('presetRange', () => {
  it('today spans the full UTC day', () => {
    const r = presetRange('today', new Date('2026-07-20T12:00:00Z'));
    expect(r.fromUtc).toBe('2026-07-20T00:00:00.000Z');
    expect(r.toUtc).toBe('2026-07-20T23:59:59.000Z');
  });
  it('7d goes back six days', () => {
    const r = presetRange('7d', new Date('2026-07-20T12:00:00Z'));
    expect(r.fromUtc).toBe('2026-07-14T00:00:00.000Z');
  });
});
```

`dateRange.ts`:

```ts
export interface DateRange { fromUtc: string; toUtc: string; }
export type RangePreset = 'today' | '7d' | '30d';

export function presetRange(preset: RangePreset, now: Date): DateRange {
  const y = now.getUTCFullYear();
  const m = now.getUTCMonth();
  const d = now.getUTCDate();
  const back = preset === 'today' ? 0 : preset === '7d' ? 6 : 29;
  const start = new Date(Date.UTC(y, m, d - back, 0, 0, 0));
  const end = new Date(Date.UTC(y, m, d, 23, 59, 59));
  return { fromUtc: start.toISOString(), toUtc: end.toISOString() };
}

export function isoToDateInput(iso: string): string { return iso.slice(0, 10); }
export function dateInputToFromUtc(date: string): string { return `${date}T00:00:00.000Z`; }
export function dateInputToToUtc(date: string): string { return `${date}T23:59:59.000Z`; }
```

Run: `bun test src/network/journal/dateRange.test.ts` → сначала FAIL, потом PASS.

- [ ] **Step 3: `orgAuditModel.ts` + падающий тест**

`orgAuditModel.test.ts`:

```ts
import { describe, it, expect } from 'bun:test';
import { toAuditRows, outcomeChipTone } from './orgAuditModel';

const rec = {
  auditRecordId: 'r1', branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
  action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
  sourceApp: 'PlatformApi', detailsJson: '{"x":1}', createdAtUtc: '2026-07-20T10:00:00Z'
};

describe('orgAuditModel', () => {
  it('maps records to rows with a system actor fallback', () => {
    const rows = toAuditRows([rec], { formatDate: (s) => s }, 'система');
    expect(rows[0].actor).toBe('система');
    expect(rows[0].target).toBe('News (n1)');
    expect(rows[0].outcomeTone).toBe('ok');
  });
  it('marks denied outcome as danger', () => {
    expect(outcomeChipTone('Denied')).toBe('danger');
  });
});
```

`orgAuditModel.ts`:

```ts
import type { OrgAuditRecordDto } from '../../api/clients/orgAudit';

export type OutcomeTone = 'ok' | 'danger' | 'muted';

export interface AuditRow {
  id: string;
  date: string;
  actor: string;
  action: string;
  target: string;
  outcome: string;
  outcomeTone: OutcomeTone;
  source: string;
  details: string;
}

export function outcomeChipTone(outcome: string): OutcomeTone {
  if (outcome === 'Succeeded') return 'ok';
  if (outcome === 'Denied') return 'danger';
  return 'muted';
}

export function toAuditRows(
  records: OrgAuditRecordDto[],
  fmt: { formatDate: (iso: string) => string },
  systemLabel: string
): AuditRow[] {
  return records.map((r) => ({
    id: r.auditRecordId,
    date: fmt.formatDate(r.createdAtUtc),
    actor: r.actorStaffUserId ?? r.actorPlatformAdminUserId ?? systemLabel,
    action: r.action,
    target: r.targetId === null ? r.targetType : `${r.targetType} (${r.targetId})`,
    outcome: r.outcome,
    outcomeTone: outcomeChipTone(r.outcome),
    source: r.sourceApp,
    details: r.detailsJson
  }));
}
```

Run: `bun test src/network/journal/orgAuditModel.test.ts` → FAIL → PASS.

- [ ] **Step 4: `useOrgAudit.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { OrgAuditQuery, OrgAuditRecordDto } from '../../api/clients/orgAudit';

export interface OrgAuditClient {
  searchOrganizationAudit(organizationId: string, query: OrgAuditQuery): Promise<{ records: OrgAuditRecordDto[] }>;
}

export type OrgAuditState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; records: OrgAuditRecordDto[]; retry: () => void };

export function useOrgAudit(client: OrgAuditClient, organizationId: string, query: OrgAuditQuery): OrgAuditState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [records, setRecords] = useState<OrgAuditRecordDto[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;
  const retry = useCallback(() => setTick((t) => t + 1), []);
  const queryKey = JSON.stringify(query);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    clientRef.current.searchOrganizationAudit(organizationId, query)
      .then((res) => { if (!cancelled) { setRecords(res.records); setPhase('ready'); } })
      .catch(() => { if (!cancelled) setPhase('error'); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [organizationId, queryKey, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading') return { status: 'loading' };
  return { status: 'ready', records, retry };
}
```

- [ ] **Step 5: `OrgAuditFilters.tsx`** (date-range пресеты + action/targetType/outcome, операторские атомы)

```tsx
import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { presetRange, isoToDateInput, dateInputToFromUtc, dateInputToToUtc, type DateRange, type RangePreset } from './dateRange';

export interface AuditDraft { action: string; outcome: string; targetType: string; }

const PRESETS: { preset: RangePreset; labelKey: 'op.network.journal.range.today' | 'op.network.journal.range.7d' | 'op.network.journal.range.30d' }[] = [
  { preset: 'today', labelKey: 'op.network.journal.range.today' },
  { preset: '7d', labelKey: 'op.network.journal.range.7d' },
  { preset: '30d', labelKey: 'op.network.journal.range.30d' }
];

export function OrgAuditFilters({ range, onRangeChange, onApply, onReset }: {
  range: DateRange;
  onRangeChange: (range: DateRange) => void;
  onApply: (draft: AuditDraft) => void;
  onReset: () => void;
}): JSX.Element {
  const { t } = useI18n();
  const [action, setAction] = useState('');
  const [outcome, setOutcome] = useState('all');
  const [targetType, setTargetType] = useState('');

  function reset() { setAction(''); setOutcome('all'); setTargetType(''); onReset(); }

  return (
    <div className="network-journal-filters mgmt-form">
      <div className="network-journal-presets">
        {PRESETS.map((p) => (
          <button key={p.preset} type="button" className="ui-btn" onClick={() => onRangeChange(presetRange(p.preset, new Date()))}>{t(p.labelKey)}</button>
        ))}
      </div>
      <div className="mgmt-form-grid">
        <label>{t('op.network.journal.range.from')}
          <input type="date" value={isoToDateInput(range.fromUtc)} onChange={(e) => onRangeChange({ fromUtc: dateInputToFromUtc(e.currentTarget.value), toUtc: range.toUtc })} />
        </label>
        <label>{t('op.network.journal.range.to')}
          <input type="date" value={isoToDateInput(range.toUtc)} onChange={(e) => onRangeChange({ fromUtc: range.fromUtc, toUtc: dateInputToToUtc(e.currentTarget.value) })} />
        </label>
        <label>{t('op.network.journal.filter.action')}
          <input value={action} onChange={(e) => setAction(e.currentTarget.value)} />
        </label>
        <label>{t('op.network.journal.filter.targetType')}
          <input value={targetType} onChange={(e) => setTargetType(e.currentTarget.value)} />
        </label>
        <label>{t('op.network.journal.filter.outcome')}
          <select value={outcome} onChange={(e) => setOutcome(e.currentTarget.value)}>
            <option value="all">{t('op.network.journal.outcome.all')}</option>
            <option value="Succeeded">{t('op.network.journal.outcome.succeeded')}</option>
            <option value="Denied">{t('op.network.journal.outcome.denied')}</option>
          </select>
        </label>
      </div>
      <div className="mgmt-form-actions">
        <button type="button" className="ui-btn ui-btn--primary" onClick={() => onApply({ action, outcome, targetType })}>{t('op.network.journal.filter.apply')}</button>
        <button type="button" className="ui-btn" onClick={reset}>{t('op.network.journal.filter.reset')}</button>
      </div>
    </div>
  );
}
```

- [ ] **Step 6: Падающий тест экрана**

`JournalDestination.test.tsx`:

```tsx
import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgAudit: {
      searchOrganizationAudit: mock(async () => ({
        records: [{
          auditRecordId: 'r1', branchId: null, actorStaffUserId: null, actorPlatformAdminUserId: null,
          action: 'news.published', targetType: 'News', targetId: 'n1', outcome: 'Succeeded',
          sourceApp: 'PlatformApi', detailsJson: '{}', createdAtUtc: '2026-07-20T10:00:00Z'
        }],
        limit: 100
      }))
    }
  })
}));

const backend = { config: { platformBaseUrl: 'x', currencyCode: 'TJS' }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };

describe('JournalDestination', () => {
  it('renders an audit row including an org-level (null-branch) action', async () => {
    const { JournalDestination } = await import('./JournalDestination');
    render(<I18nProvider initialLocale="ru"><JournalDestination backend={backend as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText('news.published')).toBeInTheDocument());
  });
});
```

- [ ] **Step 7: Прогнать — падает** — FAIL.

- [ ] **Step 8: Реализовать `JournalDestination.tsx`**

```tsx
import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../management/destinations/types';
import { presetRange, type DateRange } from './dateRange';
import { OrgAuditFilters, type AuditDraft } from './OrgAuditFilters';
import { useOrgAudit, type OrgAuditClient } from './useOrgAudit';
import { toAuditRows } from './orgAuditModel';
import type { OrgAuditQuery } from '../../api/clients/orgAudit';

const DEFAULT_LIMIT = 100;
const GRID = '1.2fr 1fr 1.4fr 1.2fr 0.8fr 0.8fr 1.4fr';

function buildQuery(range: DateRange, draft: AuditDraft): OrgAuditQuery {
  const q: OrgAuditQuery = { fromUtc: range.fromUtc, toUtc: range.toUtc, limit: DEFAULT_LIMIT };
  if (draft.action.length > 0) q.action = draft.action;
  if (draft.targetType.length > 0) q.targetType = draft.targetType;
  if (draft.outcome !== 'all') q.outcome = draft.outcome;
  return q;
}

export function JournalDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t, formatDate } = useI18n();
  const [range, setRange] = useState<DateRange>(() => presetRange('today', new Date()));
  const [query, setQuery] = useState<OrgAuditQuery>(() => buildQuery(presetRange('today', new Date()), { action: '', outcome: 'all', targetType: '' }));

  const client = useMemo<OrgAuditClient | null>(() => {
    if (backend === null) return null;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return { searchOrganizationAudit: (id, q) => clients.orgAudit.searchOrganizationAudit(id, q) };
  }, [backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useOrgAudit(
    client ?? { searchOrganizationAudit: async () => ({ records: [] }) },
    backend?.session.organizationId ?? '',
    query
  );

  const rows = state.status === 'ready' ? toAuditRows(state.records, { formatDate }, t('op.network.journal.actor.system')) : [];

  function handleRange(next: DateRange) {
    setRange(next);
    setQuery((prev) => ({ ...prev, fromUtc: next.fromUtc, toUtc: next.toUtc }));
  }

  const screenState = backend === null ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  return (
    <ManagementScreen
      title={t('op.network.dest.journal')}
      subtitle={t('op.network.dest.journal.subtitle')}
      contentWidth="full"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      <div className="network-journal">
        <OrgAuditFilters
          range={range}
          onRangeChange={handleRange}
          onApply={(draft) => setQuery(buildQuery(range, draft))}
          onReset={() => setQuery(buildQuery(range, { action: '', outcome: 'all', targetType: '' }))}
        />

        {state.status === 'loading' ? (
          <div className="management-skeleton" aria-hidden="true"><div className="management-skeleton-line" /><div className="management-skeleton-line" /><div className="management-skeleton-line" /></div>
        ) : rows.length === 0 ? (
          <EmptyState title={t('op.network.journal.empty')} />
        ) : (
          <div className="table-panel">
            <div className="ctable-head" style={{ gridTemplateColumns: GRID }} aria-hidden="true">
              <span>{t('op.network.journal.col.date')}</span>
              <span>{t('op.network.journal.col.actor')}</span>
              <span>{t('op.network.journal.col.action')}</span>
              <span>{t('op.network.journal.col.target')}</span>
              <span>{t('op.network.journal.col.outcome')}</span>
              <span>{t('op.network.journal.col.source')}</span>
              <span>{t('op.network.journal.col.details')}</span>
            </div>
            <div className="ctable-body">
              {rows.map((row) => (
                <div key={row.id} className="ctable-row" style={{ gridTemplateColumns: GRID }}>
                  <span>{row.date}</span>
                  <span>{row.actor}</span>
                  <span className="network-journal-action">{row.action}</span>
                  <span>{row.target}</span>
                  <span className={`ui-chip ui-chip--${row.outcomeTone}`}>{row.outcome}</span>
                  <span>{row.source}</span>
                  <span className="network-journal-details" title={row.details}>{row.details}</span>
                </div>
              ))}
            </div>
          </div>
        )}

        <p className="network-journal-limit-note">{t('op.network.journal.limitNote')}</p>
      </div>
    </ManagementScreen>
  );
}
```

- [ ] **Step 9: Прогнать — зелёный** — PASS.

- [ ] **Step 10: i18n-ключи Journal** (ru/en/tg)

```
op.network.journal.actor.system   "система" / "system" / "система"
op.network.journal.empty          "Записей нет" / "No records" / "Сабтҳо нестанд"
op.network.journal.limitNote      "Показаны последние записи (ограничение по количеству)." / "Showing the most recent records (count-limited)." / "Сабтҳои охирин нишон дода шудаанд (маҳдудияти шумора)."
op.network.journal.range.today    "Сегодня" / "Today" / "Имрӯз"
op.network.journal.range.7d       "7 дней" / "7 days" / "7 рӯз"
op.network.journal.range.30d      "30 дней" / "30 days" / "30 рӯз"
op.network.journal.range.from     "С" / "From" / "Аз"
op.network.journal.range.to       "По" / "To" / "То"
op.network.journal.filter.action  "Действие" / "Action" / "Амал"
op.network.journal.filter.targetType "Тип объекта" / "Target type" / "Навъи объект"
op.network.journal.filter.outcome "Итог" / "Outcome" / "Натиҷа"
op.network.journal.outcome.all    "Все" / "All" / "Ҳама"
op.network.journal.outcome.succeeded "Успех" / "Succeeded" / "Муваффақ"
op.network.journal.outcome.denied "Отказ" / "Denied" / "Рад"
op.network.journal.filter.apply   "Применить" / "Apply" / "Татбиқ"
op.network.journal.filter.reset   "Сбросить" / "Reset" / "Аз нав"
op.network.journal.col.date       "Дата" / "Date" / "Сана"
op.network.journal.col.actor      "Актёр" / "Actor" / "Иҷрокунанда"
op.network.journal.col.action     "Действие" / "Action" / "Амал"
op.network.journal.col.target     "Объект" / "Target" / "Объект"
op.network.journal.col.outcome    "Итог" / "Outcome" / "Натиҷа"
op.network.journal.col.source     "Источник" / "Source" / "Манбаъ"
op.network.journal.col.details    "Детали" / "Details" / "Тафсилот"
```

(«система» tg=ru — заимствование, в allowlist. Регенерировать + presence-блок.)

- [ ] **Step 11: Прогнать тесты + сборку** — `bun test src/network/journal/ && (i18n) && bun run build`.

- [ ] **Step 12: Commit** — `git commit -am "feat(operator): экран «Сеть → Журнал» (org-level аудит)"`.

---

## Финальная проверка (после всех тасков)

- [ ] Полный прогон бэка: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
- [ ] Полный прогон фронта (из `src/AFK4.Operator.App.Web`): `bun test $(find src -name '*.test.ts' -o -name '*.test.tsx' | grep -v App.test) && bun test src/App.test.tsx`
- [ ] i18n guard: `cd packages/i18n && bun test src/messages.test.ts`
- [ ] Сборка фронта: `bun run build`
- [ ] Whole-branch review → superpowers:finishing-a-development-branch.

## Открытые/отложенные (зафиксировать, не расширять объём)

- «Открыть филиал» с переходом на Карту из Branches — если не влезло без раздувания контракта, follow-up.
- WPF-инжект `setupInstallerUrl` в `window.__AFK4_OPERATOR_CONFIG__` — правка .NET-хоста, отдельно.
- Пагинация аудита (курсор) — вне объёма (остаётся `limit`, как в branch-версии).
