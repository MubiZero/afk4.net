# Волна B, план 2 (интерфейс) — задолженность в панели и полоса у клуба

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Довести цикл неплатежа до людей: платформа видит очередь должников с рычагами решения, а владелец клуба видит свой долг раньше, чем ему отключат обслуживание.

**Architecture:** Обе поверхности читают готовые модели с сервера, а не сшивают данные на клиенте: платформа — `GET /api/platform/debt` (одна строка на клуб в просрочке), клуб — компактный `GET /api/organizations/{id}/billing/status`. Полоса в админке клуба переиспользует существующий слот баннера (`--shell-banner-h`, где живёт `SupportModeBanner`), а не заводит второй механизм. Раздел «Задолженность» встаёт первым блоком экрана «Деньги», выше существующей очереди к оплате.

**Tech Stack:** ASP.NET Core minimal APIs + EF Core 10 (`AFK4.Platform.Api`), xUnit; React 19 + TypeScript + Vite (`AFK4.PlatformControl.Web`, `AFK4.OrganizationAdmin.Web`), `bun test` + happy-dom, общий каталог строк `@afk4/i18n`.

## Global Constraints

- Спека: `docs/superpowers/specs/2026-08-07-platform-billing-dunning-and-pricing-design.md`, разделы §7 и §8. Бэкенд цикла (слайсы 1–3) уже в `main`.
- Ветка: `feat/platform-billing-debt-ui`. Сообщения коммитов — на русском с conventional-префиксом; никаких приписок про ИИ.
- `BUN=/home/fedya/.bun/bin/bun` — bun не на PATH, вызывать полным путём.
- **Строки — только через `@afk4/i18n`.** Источник истины — `locales/ru.json`, `locales/en.json`, `locales/tg.json` в корне репозитория; после правки запускать `cd packages/i18n && "$BUN" run gen`. Обе фронтовые аппы едят один и тот же каталог: `PlatformControl.Web/src/i18n/messages.ts` — это ре-экспорт `@afk4/i18n/messages`. Хардкод-строк в компонентах быть не должно.
- Таджикский — настоящий таджикский: guard-тест падает на `tg === ru` вне whitelist заимствований.
- Зелёный `bun test` не равен зелёной сборке: `bun run build` = `tsc -b && vite build` и тайпчекает в том числе тест-файлы. Каждая фронтовая задача заканчивается сборкой.
- Деньги на границе UI: DTO отдают **минорные** единицы, `formatCurrency` ждёт **мажорные** — переводить через `minorToMajor` (`@/lib/money` в PlatformControl, `currencyFormat.ts` в OrganizationAdmin).
- Приостановка организации остаётся ручной. Ни одна кнопка этого плана не делает её автоматической.
- Org-scoped эндпоинты защищены сверкой `organizationId` из маршрута со `StaffContext.OrganizationId` (IDOR-guard) — новый эндпоинт обязан повторить эту проверку.
- Прогон: бэкенд `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~<Имя>`; фронт `cd src/AFK4.PlatformControl.Web && "$BUN" test` и `cd src/AFK4.OrganizationAdmin.Web && "$BUN" test`.

---

## File Structure

**Бэкенд (`src/AFK4.Platform.Api`)**

| Файл | Ответственность |
|---|---|
| `Platform/Billing/IDebtOverviewService.cs` | контракт чтения очереди должников |
| `Platform/Billing/EfDebtOverviewService.cs` | сборка строк: долг, дни просрочки, ступень, отсрочка |
| `Endpoints/PlatformDebtEndpoints.cs` | `GET /api/platform/debt` |
| `Endpoints/PlatformBillingEndpoints.cs` | + `GET /api/organizations/{id}/billing/status` рядом с club-side чтениями |

**Контракты (`src/AFK4.Shared.Contracts/Platform/Billing`)**: `DebtRowDto.cs`, `OrganizationBillingStatusDto.cs`.

**Панель (`src/AFK4.PlatformControl.Web/src`)**

| Файл | Ответственность |
|---|---|
| `api/platformClients/debt.ts` | сабклиент очереди должников |
| `platform/billing/DebtSection.tsx` | раздел «Задолженность» на экране «Деньги» |
| `platform/billing/useDebt.ts` | загрузка, по образцу `useInvoices.ts` |
| `platform/billing/debtModel.ts` | чистые правила: сортировка, метки ступени, признак «долг погашен, клуб отключён» |
| `platform/organizations/OrganizationDebtBlock.tsx` | тот же долг в паспорте клуба |

**Админка клуба (`src/AFK4.OrganizationAdmin.Web/src`)**

| Файл | Ответственность |
|---|---|
| `billing/BillingStatusBanner.tsx` | полоса о долге в слоте баннера |
| `billing/useBillingStatus.ts` | загрузка статуса под правом `viewSubscription` |

---

## Task 1: Читающая модель задолженности на сервере

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/DebtRowDto.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/IDebtOverviewService.cs`, `EfDebtOverviewService.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformDebtEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация сервиса и группы эндпоинтов)
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfDebtOverviewServiceTests.cs`, `tests/AFK4.Platform.Api.Tests/Platform/PlatformDebtEndpointTests.cs`

**Interfaces:**
- Consumes: `BillingBalance.Compute(IReadOnlyCollection<InvoiceEntity>)` → `OrganizationBalance(long OutstandingMinorUnits, bool InArrears, InvoiceEntity? OldestOverdue)`; поля счёта `DunningStage`, `DueAtUtc`; поле подписки `PaymentGraceUntilUtc`.
- Produces: `DebtRowDto` и `GET /api/platform/debt` — на них опираются задачи 3 и 4.

Строку собирает сервер, а не клиент: иначе панели пришлось бы тянуть два списка целиком и сшивать их по организации, а признак «долг погашен, а клуб всё ещё отключён» вообще негде было бы посчитать.

- [ ] **Step 1: Написать падающий тест сервиса**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfDebtOverviewServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-15T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static Guid SeedClub(
        PlatformDbContext db,
        string name,
        string organizationStatus = OrganizationStatusNames.Active,
        string subscriptionStatus = SubscriptionStatusNames.PastDue,
        DateTimeOffset? graceUntil = null)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = name.ToLowerInvariant(), Name = name,
            Status = organizationStatus, PlanCode = "starter", SubscriptionStatus = subscriptionStatus,
            LimitsJson = "{}", CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.OrganizationSubscriptions.Add(new OrganizationSubscriptionEntity
        {
            OrganizationSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
            Status = subscriptionStatus, CurrentPeriodStartUtc = Now.AddMonths(-1), CurrentPeriodEndUtc = Now,
            AmountMinorUnits = 290000, CurrencyCode = "TJS", BillingInterval = BillingIntervalNames.Monthly,
            PaymentGraceUntilUtc = graceUntil, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        return orgId;
    }

    private static void SeedInvoice(
        PlatformDbContext db,
        Guid orgId,
        int number,
        long amountMinorUnits,
        string status,
        DateTimeOffset dueAtUtc,
        int dunningStage = 0,
        string kind = InvoiceKindNames.Subscription) =>
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(), OrganizationId = orgId, Number = number, Kind = kind,
            PeriodStartUtc = dueAtUtc.AddMonths(-1), PeriodEndUtc = dueAtUtc, IssuedAtUtc = dueAtUtc.AddDays(-7),
            DueAtUtc = dueAtUtc, AmountMinorUnits = amountMinorUnits, GrossAmountMinorUnits = amountMinorUnits,
            CurrencyCode = "TJS", Status = status, Description = "d", DunningStage = dunningStage,
            CreatedAtUtc = dueAtUtc.AddDays(-7), UpdatedAtUtc = dueAtUtc.AddDays(-7)
        });

    [Fact]
    public async Task GetAsync_ClubInArrears_ReportsBalanceAgeAndStage()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена");
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10), dunningStage: 3);
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        var row = Assert.Single(rows);
        Assert.Equal("Арена", row.OrganizationName);
        Assert.Equal(290000, row.OutstandingMinorUnits);
        Assert.Equal(1, row.OldestOverdueInvoiceNumber);
        Assert.Equal(10, row.DaysOverdue);
        Assert.Equal(3, row.DunningStage);
        Assert.Null(row.GraceUntilUtc);
        Assert.False(row.SettledButSuspended);
    }

    [Fact]
    public async Task GetAsync_CreditNoteCoversDebt_ClubIsNotListed()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена");
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10));
        SeedInvoice(db, orgId, number: 2, amountMinorUnits: -290000, status: InvoiceStatusNames.Issued,
            dueAtUtc: Now, kind: InvoiceKindNames.Credit);
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetAsync_GraceInForce_ClubIsListedAndMarked()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", subscriptionStatus: SubscriptionStatusNames.Active,
            graceUntil: Now.AddDays(20));
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        var row = Assert.Single(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));

        Assert.Equal(Now.AddDays(20), row.GraceUntilUtc);
    }

    [Fact]
    public async Task GetAsync_SuspendedClubWithoutDebt_IsListedAsSettledButSuspended()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", organizationStatus: OrganizationStatusNames.Suspended,
            subscriptionStatus: SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Paid,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        var row = Assert.Single(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));

        Assert.True(row.SettledButSuspended);
        Assert.Equal(0, row.OutstandingMinorUnits);
    }

    [Fact]
    public async Task GetAsync_HealthyClub_IsNotListed()
    {
        await using var db = NewContext();
        var orgId = SeedClub(db, "Арена", subscriptionStatus: SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Paid,
            dueAtUtc: Now.AddDays(-10));
        await db.SaveChangesAsync();

        Assert.Empty(await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_SeveralClubs_OldestDebtComesFirst()
    {
        await using var db = NewContext();
        var fresh = SeedClub(db, "Свежий");
        var old = SeedClub(db, "Старый");
        SeedInvoice(db, fresh, number: 1, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-2));
        SeedInvoice(db, old, number: 2, amountMinorUnits: 290000, status: InvoiceStatusNames.Overdue,
            dueAtUtc: Now.AddDays(-30));
        await db.SaveChangesAsync();

        var rows = await new EfDebtOverviewService(db).GetAsync(Now, CancellationToken.None);

        Assert.Equal(["Старый", "Свежий"], rows.Select(row => row.OrganizationName).ToArray());
    }
}
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfDebtOverviewServiceTests`
Expected: FAIL — типа `EfDebtOverviewService` не существует.

- [ ] **Step 3: Объявить контракт строки**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>One club that needs a money decision: either it owes money, or it is still suspended
/// after settling. Days overdue and the dunning stage answer "how long has this been ignored".</summary>
public sealed record DebtRowDto(
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string OrganizationStatus,
    string SubscriptionStatus,
    long OutstandingMinorUnits,
    string CurrencyCode,
    int? OldestOverdueInvoiceNumber,
    Guid? OldestOverdueInvoiceId,
    int DaysOverdue,
    int DunningStage,
    DateTimeOffset? GraceUntilUtc,
    bool SettledButSuspended);
```

- [ ] **Step 4: Реализовать сервис**

```csharp
namespace AFK4.Platform.Api.Platform.Billing;

public interface IDebtOverviewService
{
    /// <summary>Clubs that need a money decision, oldest debt first.</summary>
    Task<IReadOnlyList<DebtRowDto>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
```

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfDebtOverviewService(PlatformDbContext dbContext) : IDebtOverviewService
{
    public async Task<IReadOnlyList<DebtRowDto>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unpaid = await dbContext.Invoices.AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .ToListAsync(cancellationToken);
        var unpaidByOrganization = unpaid
            .GroupBy(invoice => invoice.OrganizationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<InvoiceEntity>)group.ToList());

        var organizations = await dbContext.Organizations.AsNoTracking()
            .Where(organization => organization.Status != OrganizationStatusNames.DeletionPending)
            .ToListAsync(cancellationToken);
        var subscriptions = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .ToDictionaryAsync(subscription => subscription.OrganizationId, cancellationToken);

        var rows = new List<DebtRowDto>();
        foreach (var organization in organizations)
        {
            var invoices = unpaidByOrganization.TryGetValue(organization.OrganizationId, out var found)
                ? found
                : [];
            var balance = BillingBalance.Compute(invoices);
            var suspended = organization.Status == OrganizationStatusNames.Suspended;

            // A club that paid up but is still switched off is exactly as much a pending decision as
            // one that owes money — nothing un-suspends it automatically.
            var settledButSuspended = suspended && !balance.InArrears;
            if (!balance.InArrears && !settledButSuspended)
            {
                continue;
            }

            subscriptions.TryGetValue(organization.OrganizationId, out var subscription);
            var oldest = balance.OldestOverdue;
            rows.Add(new DebtRowDto(
                OrganizationId: organization.OrganizationId,
                OrganizationName: organization.Name,
                OrganizationSlug: organization.Slug,
                OrganizationStatus: organization.Status,
                SubscriptionStatus: organization.SubscriptionStatus,
                OutstandingMinorUnits: balance.InArrears ? balance.OutstandingMinorUnits : 0,
                CurrencyCode: oldest?.CurrencyCode ?? subscription?.CurrencyCode ?? "TJS",
                OldestOverdueInvoiceNumber: oldest?.Number,
                OldestOverdueInvoiceId: oldest?.InvoiceId,
                DaysOverdue: oldest is null ? 0 : Math.Max(0, (int)Math.Floor((now - oldest.DueAtUtc).TotalDays)),
                DunningStage: oldest?.DunningStage ?? 0,
                GraceUntilUtc: subscription?.PaymentGraceUntilUtc > now ? subscription.PaymentGraceUntilUtc : null,
                SettledButSuspended: settledButSuspended));
        }

        return rows
            .OrderByDescending(row => row.DaysOverdue)
            .ThenBy(row => row.OrganizationName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
```

- [ ] **Step 5: Убедиться, что тесты сервиса проходят**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EfDebtOverviewServiceTests`
Expected: PASS

- [ ] **Step 6: Добавить эндпоинт**

`PlatformDebtEndpoints.cs` — по образцу читающих эндпоинтов в `PlatformBillingEndpoints.cs`:

```csharp
        app.MapGet("/api/platform/debt", async (
            PlatformAdminAuthorizationService authorizationService,
            IDebtOverviewService debtOverviewService,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var rows = await debtOverviewService.GetAsync(timeProvider.GetUtcNow(), cancellationToken);
            return Results.Ok(rows);
        });
```

`PlatformAdminPermissionNames.ViewBilling` — то же право, под которым уже читаются подписки, счета и метрики в `PlatformBillingEndpoints` (проверено по коду); нового права не заводить. Зарегистрировать сервис и вызов группы в `Program.cs` рядом с существующими.

- [ ] **Step 7: Написать тест эндпоинта**

`PlatformDebtEndpointTests.cs` по образцу соседних тестов в `tests/AFK4.Platform.Api.Tests/Platform/`: клуб в просрочке виден под `platform_admin`; под ролью без права чтения биллинга — `403`; без авторизации — `401`.

- [ ] **Step 8: Прогнать тесты и закоммитить**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Debt`
Expected: PASS

```bash
git add -A
git commit -m "feat(platform): читающая модель задолженности и эндпоинт очереди должников"
```

---

## Task 2: Статус биллинга для админки клуба

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/OrganizationBillingStatusDto.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformBillingEndpoints.cs` (рядом с club-side чтениями `subscription`/`invoices`)
- Modify: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs`, `EfInvoiceService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceServiceTests.cs`, `tests/AFK4.Platform.Api.Tests/Platform/PlatformSubscriptionEndpointTests.cs`

**Interfaces:**
- Consumes: `BillingBalance.Compute` (задача 1 его не меняет).
- Produces: `OrganizationBillingStatusDto` и `GET /api/organizations/{organizationId:guid}/billing/status` — на них опирается задача 5.

Полоса рендерится на каждом экране админки, поэтому ей нужен компактный ответ, а не полный список счетов на каждой загрузке.

- [ ] **Step 1: Написать падающий тест**

Дописать в `EfInvoiceServiceTests` (хелперы `Now`, `NewContext`, `NewService`, `SeedOrganizationWithSubscriptionAsync`, `AddOverdueInvoiceAsync` — существующие в файле):

```csharp
    [Fact]
    public async Task GetBillingStatusAsync_NoDebt_ReportsNotInArrears()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.False(result.Value!.InArrears);
        Assert.Equal(0, result.Value.OutstandingMinorUnits);
        Assert.Null(result.Value.OldestOverdueInvoiceNumber);
    }

    [Fact]
    public async Task GetBillingStatusAsync_OverdueInvoice_ReportsAmountNumberAndAge()
    {
        await using var db = NewContext();
        var orgId = await SeedOrganizationWithSubscriptionAsync(db);
        await AddOverdueInvoiceAsync(db, orgId, number: 7);
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.InArrears);
        Assert.Equal(290000, result.Value.OutstandingMinorUnits);
        Assert.Equal("TJS", result.Value.CurrencyCode);
        Assert.Equal(7, result.Value.OldestOverdueInvoiceNumber);
        Assert.Equal(3, result.Value.DaysOverdue); // AddOverdueInvoiceAsync ставит срок на Now-3 дня
    }

    [Fact]
    public async Task GetBillingStatusAsync_UnknownOrganization_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = NewService(db, new FixedTimeProvider(Now));

        var result = await service.GetBillingStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }
```

Если `AddOverdueInvoiceAsync` ставит другой срок — взять фактическое число дней из кода хелпера, а не подгонять хелпер под этот листинг.

- [ ] **Step 2: Убедиться, что тест падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~GetBillingStatusAsync`
Expected: FAIL — метода не существует.

- [ ] **Step 3: Объявить контракт**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

/// <summary>Compact arrears summary for the club's own admin banner: enough to say what is owed and
/// how late it is, without pulling the whole invoice list on every screen load.</summary>
public sealed record OrganizationBillingStatusDto(
    bool InArrears,
    long OutstandingMinorUnits,
    string CurrencyCode,
    int? OldestOverdueInvoiceNumber,
    int DaysOverdue,
    DateTimeOffset? GraceUntilUtc);
```

- [ ] **Step 4: Реализовать в сервисе**

В `IInvoiceService` объявить с XML-доком:

```csharp
    Task<BillingOperationResult<OrganizationBillingStatusDto>> GetBillingStatusAsync(
        Guid organizationId,
        CancellationToken cancellationToken);
```

В `EfInvoiceService` — реализация через `BillingBalance.Compute` по неоплаченным счетам организации, срок и номер берутся у `OldestOverdue`, `GraceUntilUtc` — у подписки, если он в будущем относительно `timeProvider.GetUtcNow()`. Организация не найдена → `NotFound`.

- [ ] **Step 5: Добавить эндпоинт**

В `PlatformBillingEndpoints`, в ту же группу, где уже живут club-side `subscription` и `invoices`:

```csharp
        organizations.MapGet("billing/status", async (
            Guid organizationId,
            StaffAuthorizationService authorizationService,
            IInvoiceService invoiceService,
            CancellationToken cancellationToken) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(OrganizationPermissionNames.ViewSubscription);
            if (!authorization.IsAuthenticated)
                return Results.Unauthorized();
            if (!authorization.IsAllowed)
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            if (organizationId != authorization.StaffContext!.OrganizationId)
                return Results.StatusCode(StatusCodes.Status403Forbidden);

            var result = await invoiceService.GetBillingStatusAsync(authorization.StaffContext!.OrganizationId, cancellationToken);
            return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
        });
```

- [ ] **Step 6: Тест эндпоинта**

Дописать в `PlatformSubscriptionEndpointTests` по образцу соседних club-side тестов: сотрудник с правом `ViewSubscription` получает `200`; без права — `403`; запрос чужого `organizationId` — `403` (IDOR-guard).

- [ ] **Step 7: Прогнать и закоммитить**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Billing`
Expected: PASS

```bash
git add -A
git commit -m "feat(platform): компактный статус задолженности для админки клуба"
```

---

## Task 3: Раздел «Задолженность» в панели платформы

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/debt.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/billing/useDebt.ts`, `debtModel.ts`, `DebtSection.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts` (тип `DebtRow`), `api/platformApi.ts` (подключить сабклиент)
- Modify: `src/AFK4.PlatformControl.Web/src/platform/billing/BillingScreen.tsx`, `billingModel.ts`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `platform/billing/debtModel.test.ts`, `platform/billing/DebtSection.test.tsx`

**Interfaces:**
- Consumes: `GET /api/platform/debt` → `DebtRowDto` (задача 1).
- Produces: `DebtSection` — используется в `BillingScreen`; `debtModel` переиспользуется задачей 4.

Раздел встаёт **первым** блоком экрана «Деньги», выше очереди к оплате: очередь отвечает на «какие счета не оплачены», а раздел — на «какие клубы требуют решения», и это более крупный вопрос.

- [ ] **Step 1: Написать падающий тест модели**

```ts
import { expect, it } from 'bun:test';
import { dunningStageLabelKey, sortDebtRows, debtTotals } from './debtModel';
import type { DebtRow } from '@/api/types';

function row(overrides: Partial<DebtRow> = {}): DebtRow {
  return {
    organizationId: 'o1',
    organizationName: 'Арена',
    organizationSlug: 'arena',
    organizationStatus: 'active',
    subscriptionStatus: 'past_due',
    outstandingMinorUnits: 290000,
    currencyCode: 'TJS',
    oldestOverdueInvoiceNumber: 1,
    oldestOverdueInvoiceId: 'i1',
    daysOverdue: 10,
    dunningStage: 3,
    graceUntilUtc: null,
    settledButSuspended: false,
    ...overrides
  };
}

it('ставит самый старый долг первым', () => {
  const rows = sortDebtRows([row({ organizationName: 'Свежий', daysOverdue: 2 }), row({ organizationName: 'Старый', daysOverdue: 30 })]);
  expect(rows.map(r => r.organizationName)).toEqual(['Старый', 'Свежий']);
});

it('складывает долг по валютам', () => {
  const totals = debtTotals([row({ outstandingMinorUnits: 290000 }), row({ outstandingMinorUnits: 100000 })]);
  expect(totals).toEqual([{ currencyCode: 'TJS', amountMinorUnits: 390000 }]);
});

it('не считает в итог клуб без долга, оставшийся отключённым', () => {
  const totals = debtTotals([row({ outstandingMinorUnits: 0, settledButSuspended: true })]);
  expect(totals).toEqual([]);
});

it('переводит ступень напоминаний в ключ строки', () => {
  expect(dunningStageLabelKey(0)).toBe('platform.debt.stage.none');
  expect(dunningStageLabelKey(4)).toBe('platform.debt.stage.final');
});
```

- [ ] **Step 2: Убедиться, что тест падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test debtModel`
Expected: FAIL — модуля `debtModel` нет.

- [ ] **Step 3: Реализовать модель, тип и сабклиент**

`api/types.ts` — тип `DebtRow` зеркалит `DebtRowDto` (camelCase, `Guid` → `string`, `DateTimeOffset?` → `string | null`).

`api/platformClients/debt.ts` — сабклиент с одним методом `listDebt(): Promise<DebtRow[]>`, по образцу соседнего `invoices.ts`; подключить в `platformApi.ts` как `debt`.

`platform/billing/debtModel.ts` — чистые функции `sortDebtRows`, `debtTotals`, `dunningStageLabelKey` (0 → `platform.debt.stage.none`, 1 → `.first`, 2 → `.second`, 3 → `.third`, 4 → `.final`; типизировать возврат как `MessageKey`).

`platform/billing/useDebt.ts` — загрузка по образцу `useInvoices.ts` (тот же контракт состояний `loading`/`error`/`ready` с `retry`).

- [ ] **Step 4: Убедиться, что тест модели проходит**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test debtModel`
Expected: PASS

- [ ] **Step 5: Добавить строки в каталог**

В `locales/ru.json`, `en.json`, `tg.json` — ключи раздела: заголовок и подпись, пустое состояние («никто не должен» — это хорошая новость, так и написать), колонки (клуб, долг, дней просрочки, ступень, отсрочка), пять названий ступеней, метка «отсрочка до {date}», метка «долг погашен, клуб отключён», названия действий (отметить оплаченным, дать отсрочку, отключить, заметка).

Заодно дописать недостающие виды счёта в `INVOICE_KIND_LABEL` (`billingModel.ts`) и их строки: `one_off` и `credit` появились в предыдущем плане, но в панели показываются пустой ячейкой.

Затем: `cd packages/i18n && "$BUN" run gen`.

Таджикский — настоящий перевод, не копия русского.

- [ ] **Step 6: Написать тест раздела**

`DebtSection.test.tsx` по образцу соседних тестов вкладок: рендерит строку клуба с суммой, днями просрочки и ступенью; показывает пустое состояние, когда должников нет; помечает строку с действующей отсрочкой; помечает клуб «долг погашен, но отключён»; действия не отображаются, когда `canManage === false`.

- [ ] **Step 7: Реализовать раздел и встроить в экран**

`DebtSection.tsx` — карточка по образцу `PayableQueue.tsx`: заголовок с итогом по валютам, строки клубов, действия из строки (отметить оплаченным, дать отсрочку, отключить, заметка) под `canManage`. Кнопка отключения ведёт в тот же подтверждающий диалог с причиной, что уже используется в паспорте клуба, — новую механику приостановки не изобретать.

В `BillingScreen.tsx` вставить `<DebtSection client={client} canManage={canManage} />` первым блоком, выше `PayableQueue`.

- [ ] **Step 8: Прогнать тесты и сборку**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build`
Expected: PASS, сборка без ошибок типов.

- [ ] **Step 9: Коммит**

```bash
git add -A
git commit -m "feat(platform-control): раздел задолженности на экране «Деньги»"
```

---

## Task 4: Долг в паспорте клуба

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationDebtBlock.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/ClientPassport.tsx`
- Test: `platform/organizations/OrganizationDebtBlock.test.tsx`

**Interfaces:**
- Consumes: `debtModel` и сабклиент `debt` (задача 3).

Паспорт уже показывает признак просрочки (`isPastDue`), но не отвечает, сколько именно и сколько дней — человек, открывший карточку клуба, вынужден уходить на другой экран.

- [ ] **Step 1: Написать падающий тест**

`OrganizationDebtBlock.test.tsx`: блок показывает сумму долга, номер самого старого просроченного счёта и дни просрочки; при отсутствии долга показывает спокойное состояние без тревожной подсветки; при действующей отсрочке показывает дату её окончания.

- [ ] **Step 2: Убедиться, что тест падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test OrganizationDebtBlock`
Expected: FAIL — компонента нет.

- [ ] **Step 3: Реализовать блок и встроить в паспорт**

`OrganizationDebtBlock.tsx` — принимает строку долга (или `null`) и рендерит сумму, номер счёта, дни просрочки, отсрочку. Строки берутся из ключей, добавленных в задаче 3; новых ключей по возможности не заводить.

В `ClientPassport.tsx` — загрузка строки долга для этой организации и вставка блока рядом с существующим блоком подписки. Ошибка загрузки не должна ломать паспорт: соседние эффекты глушат её и остаются полезными без части данных — держаться того же поведения.

- [ ] **Step 4: Прогнать тесты и сборку**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build`
Expected: PASS

- [ ] **Step 5: Коммит**

```bash
git add -A
git commit -m "feat(platform-control): блок задолженности в паспорте клуба"
```

---

## Task 5: Полоса о долге в админке клуба

**Files:**
- Create: `src/AFK4.OrganizationAdmin.Web/src/billing/useBillingStatus.ts`, `billing/BillingStatusBanner.tsx`
- Modify: `src/AFK4.OrganizationAdmin.Web/src/App.tsx`, `operatorApiClients.ts`, `operatorTypes.ts`
- Modify: `locales/ru.json`, `en.json`, `tg.json`
- Test: `billing/BillingStatusBanner.test.tsx`, `billing/useBillingStatus.test.ts`

**Interfaces:**
- Consumes: `GET /api/organizations/{organizationId:guid}/billing/status` → `OrganizationBillingStatusDto` (задача 2); `hasPermission(session, permissionNames.viewSubscription)` из `operatorPermissions.ts`.

- [ ] **Step 1: Написать падающий тест полосы**

`BillingStatusBanner.test.tsx` по образцу `support/SupportModeBanner.test.tsx` (тот же `I18nProvider`, тот же стиль): полоса показывает номер счёта, сумму и дни просрочки; при действующей отсрочке текст меняется на спокойный с датой; полоса не рендерится, когда долга нет.

- [ ] **Step 2: Убедиться, что тест падает**

Run: `cd src/AFK4.OrganizationAdmin.Web && "$BUN" test BillingStatusBanner`
Expected: FAIL — компонента нет.

- [ ] **Step 3: Добавить строки в каталог**

В `locales/*.json` — текст полосы: «Счёт №{number} просрочен на {days} дн., к оплате {amount}» и вариант с отсрочкой «Оплата отсрочена до {date}». Через ICU, с правильными формами множественного числа для дней в каждом языке — не склеивать строку из кусков в JSX.

Затем `cd packages/i18n && "$BUN" run gen`.

- [ ] **Step 4: Реализовать загрузку и полосу**

`useBillingStatus.ts` — загружает статус, только когда пользователь авторизован и у него есть право `viewSubscription`; иначе возвращает `null` и запрос не делает. Кассиру суммы платформы видеть незачем, и лишний запрос на каждой загрузке ему тоже незачем.

`BillingStatusBanner.tsx` — по образцу `SupportModeBanner.tsx`: полоса в том же слоте, не закрывается кликом, спокойная по тону при действующей отсрочке и тревожная без неё.

- [ ] **Step 5: Встроить в оболочку**

В `App.tsx`: полоса рендерится в существующем слоте баннера и учитывается в `--shell-banner-h` так же, как `SupportModeBanner`. **В режиме поддержки полоса о долге не показывается** — там экран держит сотрудник платформы, который видит тот же долг в своей панели, а два баннера друг над другом ломают высоту оболочки.

- [ ] **Step 6: Прогнать тесты и сборку**

Run: `cd src/AFK4.OrganizationAdmin.Web && "$BUN" test && "$BUN" run build`
Expected: PASS

- [ ] **Step 7: Прогнать guard-тесты каталога строк**

Run: `cd packages/i18n && "$BUN" test`
Expected: PASS — в том числе страж «таджикский не равен русскому» и проверка, что все использованные ключи существуют.

- [ ] **Step 8: Коммит**

```bash
git add -A
git commit -m "feat(operator): полоса о просроченном счёте в админке клуба"
```

---

## Проверка плана

- Спека §7 (раздел «Задолженность», действия из строки, метка «долг погашен, клуб отключён», блок в паспорте) — задачи 1, 3, 4.
- Спека §8 (полоса у клуба, право `ViewSubscription`, компактный эндпоинт вместо списка счетов) — задачи 2 и 5.
- Виды счёта `one_off` и `credit` без подписи в панели — закрыто в задаче 3 (шаг 5).
- Приостановка организации нигде не становится автоматической: раздел лишь ведёт в существующий подтверждающий диалог.
