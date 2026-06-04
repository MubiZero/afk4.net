# Platform Billing Admin UI (SP3 Plan 4) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the `/admin/billing` control-plane area (Подписки / Инвойсы / Тарифы), add subscription & invoice sections to the tenant-detail drawer, and surface billing KPIs (MRR / outstanding / overdue) on the Platform Overview — on top of the Plan-3 billing backend plus three new cross-tenant read endpoints added here.

**Architecture:** Full-stack (owner decision 2026-05-31). Backend: three new read-only platform endpoints (`GET /api/platform/subscriptions`, `GET /api/platform/invoices`, `GET /api/platform/metrics`) implemented by extending the existing `ITenantSubscriptionService` / `IInvoiceService` and a new `IBillingMetricsService`, all xUnit-covered. Frontend: a new `src/platform/billing/` feature module mirroring `src/platform/tenants/` (pure `*Model.ts` builders + discriminated-union `use*` hooks + presentational screens), new `PlatformApiClient` billing methods (with a from-scratch `Idempotency-Key` mechanism for the write actions), a `TenantSubscriptionSection` (replacing the legacy `TenantPlanSection`, migrating the tenant-detail plan UI from `PATCH /plan` to `PATCH /subscription`) and a `TenantInvoicesSection`, billing KPI tiles on Overview, and the `/admin/billing` route + nav flip.

**Tech Stack:** ASP.NET minimal APIs + EF Core (`PlatformDbContext`) + xUnit (`PlatformApiFactory`); React + TypeScript + Tailwind v4 + shadcn-style primitives (`@/components/ui/*`) + Vitest (`globals:false`).

---

## Conventions (read before starting)

- **Build gate is `npm run build` (`tsc -b && vite build`), NOT `npm test`.** Vitest/esbuild skips type-checks, so a green `npm test` can still have type errors. Run **both** `npm run build` and `npm test` at every frontend checkpoint. Frontend commands run from `src/AFK4.Platform.Web`.
- **Backend gate:** from repo root, `dotnet build` then `dotnet test` (the suite uses `PlatformApiFactory` in-memory EF; current baseline is 586 passing tests).
- **Money = minor units + `currencyCode`** everywhere. Format in the UI with `formatCurrency` from `useI18n()`.
- **Vitest imports are explicit** (`globals:false`): `import { describe, it, expect, vi } from 'vitest';`.
- **i18n parity is enforced** by `src/i18n/messages.test.ts`. Every key you add to the `ru` block MUST also be added to the `en` block, or the suite fails. `MessageKey` is `keyof (typeof messages)['ru']`.
- **`src/preview/DemoApp.tsx` is the user's UNTRACKED scratch.** Do not `git add` it. If a shared prop contract changes and breaks `tsc -b` there, fix it in the working tree but leave it untracked. This plan does not change shared shell contracts, so it should not be affected.
- **Backend `BillingOperationResult<T>` requires `where T : class`.** `IReadOnlyList<...>` and DTO records satisfy this; do not wrap a bare `int`/`long`.
- **Existing billing permission constants (reuse, do not invent):** `PlatformAdminPermissionNames.ViewBilling` = `"platform.billing.view"`, `.ManagePlans` = `"platform.billing.plans.manage"`, `.ManageSubscriptions` = `"platform.billing.subscriptions.manage"`, `.ManageInvoices` = `"platform.billing.invoices.manage"`. The new read endpoints all use `ViewBilling`.
- **Existing audit action constants (reuse):** `AuditActionNames.ViewBilling` covers the new GET list/metrics endpoints (denial audit only, mirroring the existing `GET /plans` handler).

---

## File Structure

**Backend — `src/AFK4.Shared.Contracts/Platform/Billing/` (new contract DTOs):**
- `SubscriptionListItemDto.cs` — cross-tenant subscription row (embeds org identity).
- `InvoiceListItemDto.cs` — cross-tenant invoice row (embeds org identity).
- `PlatformBillingMetricsDto.cs` — MRR / outstanding / overdue aggregates.

**Backend — `src/AFK4.Platform.Api/Platform/Billing/`:**
- `ITenantSubscriptionService.cs` / `EfTenantSubscriptionService.cs` — add `ListAsync(status, planCode)`.
- `IInvoiceService.cs` / `EfInvoiceService.cs` — add `ListAllAsync(status)`.
- `IBillingMetricsService.cs` / `EfBillingMetricsService.cs` — new; `GetAsync()`.

**Backend — `src/AFK4.Platform.Api/Program.cs`:** three new `app.MapGet` handlers + one DI line.

**Backend — `tests/AFK4.Platform.Api.Tests/Platform/`:**
- `BillingListEndpointTests.cs` — new (subscriptions + invoices list endpoints).
- `BillingMetricsTests.cs` — new (metrics service + endpoint).

**Frontend — `src/AFK4.Platform.Web/src/api/`:**
- `types.ts` — add billing DTO interfaces + name constants.
- `platformApi.ts` — add billing client methods + `Idempotency-Key` support.

**Frontend — `src/AFK4.Platform.Web/src/platform/billing/` (new module):**
- `billingModel.ts` (+ `.test.ts`) — pure label/variant maps, filters, MRR/plan-form helpers.
- `usePlans.ts` (+ `.test.tsx`), `useSubscriptions.ts` (+ `.test.tsx`), `useInvoices.ts` (+ `.test.tsx`), `useBillingMetrics.ts` (+ `.test.tsx`).
- `PlanFormDialog.tsx` (+ `.test.tsx`), `PlansTab.tsx`, `SubscriptionsTab.tsx`, `InvoicesTab.tsx` (+ `.test.tsx`), `BillingScreen.tsx` (+ `.test.tsx`).

**Frontend — `src/AFK4.Platform.Web/src/platform/tenants/`:**
- `TenantSubscriptionSection.tsx` (+ `.test.tsx`) — new; replaces `TenantPlanSection`.
- `TenantInvoicesSection.tsx` (+ `.test.tsx`) — new.
- `TenantDrawer.tsx` — swap section composition.
- `TenantPlanSection.tsx` + `TenantPlanSection.test.tsx` — **deleted**.

**Frontend — Overview / nav / routing:**
- `src/platform/overview/OverviewScreen.tsx` — add billing KPI tiles + `metrics` prop.
- `src/platform/nav.ts` — flip `billing` `soon: false`.
- `src/App.tsx` — add `adminBilling` route kind, routing, screen title, path resolution.
- `src/i18n/messages.ts` — `platform.billing.*` + new tenant-section keys (ru + en).

---

## Phase A — Backend cross-tenant read endpoints

### Task 1: Cross-tenant contract DTOs

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/SubscriptionListItemDto.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/InvoiceListItemDto.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/PlatformBillingMetricsDto.cs`

- [ ] **Step 1: Create `SubscriptionListItemDto.cs`**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record SubscriptionListItemDto(
    Guid TenantSubscriptionId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    string PlanCode,
    string Status,
    string BillingInterval,
    long AmountMinorUnits,
    string CurrencyCode,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? NextInvoiceUtc,
    bool CancelAtPeriodEnd);
```

- [ ] **Step 2: Create `InvoiceListItemDto.cs`**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record InvoiceListItemDto(
    Guid InvoiceId,
    Guid OrganizationId,
    string OrganizationName,
    string OrganizationSlug,
    int Number,
    string Kind,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset DueAtUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string Status);
```

- [ ] **Step 3: Create `PlatformBillingMetricsDto.cs`**

```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record PlatformBillingMetricsDto(
    long MrrMinorUnits,
    string CurrencyCode,
    int ActiveSubscriptions,
    long OutstandingMinorUnits,
    int OutstandingCount,
    long OverdueMinorUnits,
    int OverdueCount);
```

- [ ] **Step 4: Build**

Run: `dotnet build src/AFK4.Shared.Contracts/AFK4.Shared.Contracts.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Shared.Contracts/Platform/Billing/SubscriptionListItemDto.cs src/AFK4.Shared.Contracts/Platform/Billing/InvoiceListItemDto.cs src/AFK4.Shared.Contracts/Platform/Billing/PlatformBillingMetricsDto.cs
git commit -m "feat(platform): cross-tenant billing list + metrics contracts"
```

---

### Task 2: `ITenantSubscriptionService.ListAsync` (cross-tenant subscriptions)

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Billing/ITenantSubscriptionService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfTenantSubscriptionService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs` (create in this task; reused in Task 4)

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs`. (This first test targets the service directly via a fresh `PlatformDbContext` from the factory's service provider; HTTP-level tests are added in Task 4.)

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class BillingListEndpointTests(PlatformApiFactory factory)
    : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task ListSubscriptions_returns_rows_with_org_identity()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var org = await SeedOrgWithSubscriptionAsync(db, "acme", "Acme", SubscriptionStatusNames.Active);

        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();
        var result = await service.ListAsync(status: null, planCode: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        var row = Assert.Single(result.Value!, r => r.OrganizationId == org);
        Assert.Equal("Acme", row.OrganizationName);
        Assert.Equal("acme", row.OrganizationSlug);
    }

    [Fact]
    public async Task ListSubscriptions_filters_by_status()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        await SeedOrgWithSubscriptionAsync(db, "alpha-active", "Alpha", SubscriptionStatusNames.Active);
        await SeedOrgWithSubscriptionAsync(db, "beta-cancelled", "Beta", SubscriptionStatusNames.Cancelled);

        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();
        var result = await service.ListAsync(status: SubscriptionStatusNames.Cancelled, planCode: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.All(result.Value!, r => Assert.Equal(SubscriptionStatusNames.Cancelled, r.Status));
        Assert.Contains(result.Value!, r => r.OrganizationSlug == "beta-cancelled");
        Assert.DoesNotContain(result.Value!, r => r.OrganizationSlug == "alpha-active");
    }

    [Fact]
    public async Task ListSubscriptions_rejects_unknown_status()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ITenantSubscriptionService>();

        var result = await service.ListAsync(status: "bogus", planCode: null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    internal static async Task<Guid> SeedOrgWithSubscriptionAsync(
        PlatformDbContext db, string slug, string name, string status)
    {
        var orgId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId,
            Slug = slug,
            Name = name,
            Status = "active",
            PlanCode = "starter",
            SubscriptionStatus = status,
            LimitsJson = "{}",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = "starter",
            Status = status,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = now.AddMonths(1),
            NextInvoiceUtc = now.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
        return orgId;
    }
}
```

> NOTE: If `OrganizationEntity` requires additional non-nullable columns the compiler will flag them — add them to the seed object using the same defaults as `EfTenantSubscriptionService.EnsureSubscriptionAsync` / the existing tenant-create path. Do not change the entity.

- [ ] **Step 2: Add the interface method**

In `ITenantSubscriptionService.cs`, add inside the interface:

```csharp
    Task<BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>> ListAsync(
        string? status,
        string? planCode,
        CancellationToken cancellationToken);
```

- [ ] **Step 3: Implement in `EfTenantSubscriptionService.cs`**

Add this method to the class (it reuses the existing `AllowedStatuses` set):

```csharp
    public async Task<BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>> ListAsync(
        string? status,
        string? planCode,
        CancellationToken cancellationToken)
    {
        if (status is not null && !AllowedStatuses.Contains(status.Trim()))
        {
            return BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>.BadRequest(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        var query =
            from subscription in dbContext.TenantSubscriptions.AsNoTracking()
            join org in dbContext.Organizations.AsNoTracking()
                on subscription.OrganizationId equals org.OrganizationId
            select new { subscription, org.Name, org.Slug };

        if (status is not null)
        {
            var s = status.Trim();
            query = query.Where(x => x.subscription.Status == s);
        }

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            var p = planCode.Trim();
            query = query.Where(x => x.subscription.PlanCode == p);
        }

        var rows = await query
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        IReadOnlyList<SubscriptionListItemDto> dtos = rows.Select(x => new SubscriptionListItemDto(
            TenantSubscriptionId: x.subscription.TenantSubscriptionId,
            OrganizationId: x.subscription.OrganizationId,
            OrganizationName: x.Name,
            OrganizationSlug: x.Slug,
            PlanCode: x.subscription.PlanCode,
            Status: x.subscription.Status,
            BillingInterval: x.subscription.BillingInterval,
            AmountMinorUnits: x.subscription.AmountMinorUnits,
            CurrencyCode: x.subscription.CurrencyCode,
            CurrentPeriodEndUtc: x.subscription.CurrentPeriodEndUtc,
            NextInvoiceUtc: x.subscription.NextInvoiceUtc,
            CancelAtPeriodEnd: x.subscription.CancelAtPeriodEnd)).ToList();

        return BillingOperationResult<IReadOnlyList<SubscriptionListItemDto>>.Success(dtos);
    }
```

- [ ] **Step 4: Run the test**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingListEndpointTests.ListSubscriptions`
Expected: 3 passing.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/ITenantSubscriptionService.cs src/AFK4.Platform.Api/Platform/Billing/EfTenantSubscriptionService.cs tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs
git commit -m "feat(platform): cross-tenant subscription listing service"
```

---

### Task 3: `IInvoiceService.ListAllAsync` (cross-tenant invoices)

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs` (extend)

- [ ] **Step 1: Add failing tests**

Append to `BillingListEndpointTests`:

```csharp
    [Fact]
    public async Task ListInvoices_returns_rows_with_org_identity_newest_first()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = await SeedOrgWithSubscriptionAsync(db, "inv-org", "Invoice Org", SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 1, status: InvoiceStatusNames.Issued);
        SeedInvoice(db, orgId, number: 2, status: InvoiceStatusNames.Paid);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var result = await service.ListAllAsync(status: null, CancellationToken.None);

        Assert.True(result.Succeeded);
        var mine = result.Value!.Where(r => r.OrganizationId == orgId).ToList();
        Assert.Equal(2, mine.Count);
        Assert.Equal(2, mine[0].Number); // newest (highest number) first
        Assert.Equal("Invoice Org", mine[0].OrganizationName);
    }

    [Fact]
    public async Task ListInvoices_filters_by_status_and_rejects_unknown()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orgId = await SeedOrgWithSubscriptionAsync(db, "inv-filter", "Filter Org", SubscriptionStatusNames.Active);
        SeedInvoice(db, orgId, number: 10, status: InvoiceStatusNames.Overdue);
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IInvoiceService>();
        var overdue = await service.ListAllAsync(status: InvoiceStatusNames.Overdue, CancellationToken.None);
        Assert.True(overdue.Succeeded);
        Assert.All(overdue.Value!, r => Assert.Equal(InvoiceStatusNames.Overdue, r.Status));

        var bad = await service.ListAllAsync(status: "nope", CancellationToken.None);
        Assert.False(bad.Succeeded);
        Assert.Equal(BillingOperationStatus.BadRequest, bad.Status);
    }

    private static void SeedInvoice(PlatformDbContext db, Guid orgId, int number, string status)
    {
        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            Status = status,
            Description = "Test invoice",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }
```

- [ ] **Step 2: Add the interface method**

In `IInvoiceService.cs` add:

```csharp
    Task<BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>> ListAllAsync(
        string? status,
        CancellationToken cancellationToken);
```

- [ ] **Step 3: Implement in `EfInvoiceService.cs`**

Add to the class (reuses the existing `AllowedStatusFilters` set):

```csharp
    public async Task<BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>> ListAllAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        string? normalized = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            normalized = status.Trim();
            if (!AllowedStatusFilters.Contains(normalized))
            {
                return BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>.BadRequest(
                    $"status must be one of: {string.Join(", ", AllowedStatusFilters)}.");
            }
        }

        var query =
            from invoice in dbContext.Invoices.AsNoTracking()
            join org in dbContext.Organizations.AsNoTracking()
                on invoice.OrganizationId equals org.OrganizationId
            select new { invoice, org.Name, org.Slug };

        if (normalized is not null)
        {
            query = query.Where(x => x.invoice.Status == normalized);
        }

        var rows = await query
            .OrderByDescending(x => x.invoice.Number)
            .ToListAsync(cancellationToken);

        IReadOnlyList<InvoiceListItemDto> dtos = rows.Select(x => new InvoiceListItemDto(
            InvoiceId: x.invoice.InvoiceId,
            OrganizationId: x.invoice.OrganizationId,
            OrganizationName: x.Name,
            OrganizationSlug: x.Slug,
            Number: x.invoice.Number,
            Kind: x.invoice.Kind,
            IssuedAtUtc: x.invoice.IssuedAtUtc,
            DueAtUtc: x.invoice.DueAtUtc,
            AmountMinorUnits: x.invoice.AmountMinorUnits,
            CurrencyCode: x.invoice.CurrencyCode,
            Status: x.invoice.Status)).ToList();

        return BillingOperationResult<IReadOnlyList<InvoiceListItemDto>>.Success(dtos);
    }
```

- [ ] **Step 4: Run**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingListEndpointTests.ListInvoices`
Expected: 2 passing.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs src/AFK4.Platform.Api/Platform/Billing/EfInvoiceService.cs tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs
git commit -m "feat(platform): cross-tenant invoice listing service"
```

---

### Task 4: Cross-tenant list endpoints (`GET /subscriptions`, `GET /invoices`)

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (add two `MapGet` handlers after the `GET .../invoices` handler at line ~1785, before the `mark-paid` handler at line ~1869)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs` (extend with HTTP-level tests)

- [ ] **Step 1: Add failing HTTP tests**

Append to `BillingListEndpointTests`. (Uses `PlatformAdminTestHelper.AuthorizeAsAsync`, the established auth helper.)

```csharp
    [Fact]
    public async Task GET_subscriptions_requires_auth()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/platform/subscriptions");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscriptions_returns_rows_when_authorized()
    {
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var rows = await client.GetFromJsonAsync<List<SubscriptionListItemDto>>("/api/platform/subscriptions");
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task GET_invoices_returns_rows_when_authorized()
    {
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var rows = await client.GetFromJsonAsync<List<InvoiceListItemDto>>("/api/platform/invoices");
        Assert.NotNull(rows);
    }

    [Fact]
    public async Task GET_subscriptions_rejects_bad_status_filter()
    {
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync("/api/platform/subscriptions?status=bogus");
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, response.StatusCode);
    }
```

Add the required usings at the top of the file if missing: `using System.Net.Http.Json;`.

- [ ] **Step 2: Add the `GET /api/platform/subscriptions` handler**

In `Program.cs`, immediately after the `GET /api/platform/tenants/{organizationId:guid}/invoices` handler (closes at line ~1785), insert:

```csharp
app.MapGet("/api/platform/subscriptions", async (
    string? status,
    string? planCode,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "TenantSubscription",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await subscriptionService.ListAsync(status, planCode, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});
```

- [ ] **Step 3: Add the `GET /api/platform/invoices` handler**

Directly below the handler from Step 2, insert:

```csharp
app.MapGet("/api/platform/invoices", async (
    string? status,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "Invoice",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var result = await invoiceService.ListAllAsync(status, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});
```

> NOTE: `GET /api/platform/invoices` (collection) must be registered; it does not collide with the existing `POST /api/platform/invoices/{invoiceId:guid}/{mark-paid,void}` routes (different verb + path).

- [ ] **Step 4: Run**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingListEndpointTests`
Expected: all green (Tasks 2–4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs
git commit -m "feat(platform): GET /subscriptions and /invoices cross-tenant endpoints"
```

---

### Task 5: Billing metrics service + `GET /api/platform/metrics`

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/IBillingMetricsService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfBillingMetricsService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration + `MapGet` handler)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/BillingMetricsTests.cs`

**Metric definitions (locked):**
- **MRR** = Σ over subscriptions with `Status == active` of monthly-normalized amount: `monthly` → `AmountMinorUnits`; `yearly` → `AmountMinorUnits / 12` (integer division). `CurrencyCode` = currency of the first active subscription, else `"RUB"`. (Mixed-currency summing is out of scope — all seeded plans are RUB; documented assumption.)
- **Outstanding** = Σ `AmountMinorUnits` of invoices with `Status ∈ {issued, overdue}`; `OutstandingCount` = their count.
- **Overdue** = the `Status == overdue` subset of outstanding.

- [ ] **Step 1: Write the failing test**

Create `tests/AFK4.Platform.Api.Tests/Platform/BillingMetricsTests.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class BillingMetricsTests(PlatformApiFactory factory)
    : IClassFixture<PlatformApiFactory>
{
    [Fact]
    public async Task Metrics_sum_mrr_outstanding_and_overdue()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        // One active monthly subscription @ 290000 + one cancelled (excluded from MRR).
        var activeOrg = await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
            db, "mrr-active", "MRR Active", SubscriptionStatusNames.Active);
        await BillingListEndpointTests.SeedOrgWithSubscriptionAsync(
            db, "mrr-cancelled", "MRR Cancelled", SubscriptionStatusNames.Cancelled);

        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(MakeInvoice(activeOrg, 1001, InvoiceStatusNames.Issued, 290000, now));
        db.Invoices.Add(MakeInvoice(activeOrg, 1002, InvoiceStatusNames.Overdue, 150000, now));
        db.Invoices.Add(MakeInvoice(activeOrg, 1003, InvoiceStatusNames.Paid, 999999, now)); // excluded
        await db.SaveChangesAsync();

        var service = scope.ServiceProvider.GetRequiredService<IBillingMetricsService>();
        var metrics = await service.GetAsync(CancellationToken.None);

        Assert.True(metrics.MrrMinorUnits >= 290000);
        Assert.Equal("RUB", metrics.CurrencyCode);
        Assert.True(metrics.ActiveSubscriptions >= 1);
        Assert.True(metrics.OutstandingMinorUnits >= 290000 + 150000);
        Assert.True(metrics.OutstandingCount >= 2);
        Assert.True(metrics.OverdueMinorUnits >= 150000);
        Assert.True(metrics.OverdueCount >= 1);
    }

    private static InvoiceEntity MakeInvoice(Guid orgId, int number, string status, long amount, DateTimeOffset now) =>
        new()
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = orgId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = amount,
            CurrencyCode = "RUB",
            Status = status,
            Description = "metrics test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

    [Fact]
    public async Task GET_metrics_returns_payload_when_authorized()
    {
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var metrics = await client.GetFromJsonAsync<PlatformBillingMetricsDto>("/api/platform/metrics");
        Assert.NotNull(metrics);
    }

    [Fact]
    public async Task GET_metrics_requires_auth()
    {
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/platform/metrics");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

Add `using System.Net.Http.Json;` at the top.

- [ ] **Step 2: Create `IBillingMetricsService.cs`**

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IBillingMetricsService
{
    Task<PlatformBillingMetricsDto> GetAsync(CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Create `EfBillingMetricsService.cs`**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfBillingMetricsService(PlatformDbContext dbContext) : IBillingMetricsService
{
    private const string DefaultCurrency = "RUB";

    public async Task<PlatformBillingMetricsDto> GetAsync(CancellationToken cancellationToken)
    {
        var activeSubscriptions = await dbContext.TenantSubscriptions.AsNoTracking()
            .Where(s => s.Status == SubscriptionStatusNames.Active)
            .Select(s => new { s.AmountMinorUnits, s.BillingInterval, s.CurrencyCode })
            .ToListAsync(cancellationToken);

        long mrr = 0;
        foreach (var s in activeSubscriptions)
        {
            mrr += s.BillingInterval == BillingIntervalNames.Yearly
                ? s.AmountMinorUnits / 12
                : s.AmountMinorUnits;
        }

        var currency = activeSubscriptions.Count > 0 ? activeSubscriptions[0].CurrencyCode : DefaultCurrency;

        var outstanding = await dbContext.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatusNames.Issued || i.Status == InvoiceStatusNames.Overdue)
            .Select(i => new { i.AmountMinorUnits, i.Status })
            .ToListAsync(cancellationToken);

        var outstandingTotal = outstanding.Sum(i => i.AmountMinorUnits);
        var overdue = outstanding.Where(i => i.Status == InvoiceStatusNames.Overdue).ToList();

        return new PlatformBillingMetricsDto(
            MrrMinorUnits: mrr,
            CurrencyCode: currency,
            ActiveSubscriptions: activeSubscriptions.Count,
            OutstandingMinorUnits: outstandingTotal,
            OutstandingCount: outstanding.Count,
            OverdueMinorUnits: overdue.Sum(i => i.AmountMinorUnits),
            OverdueCount: overdue.Count);
    }
}
```

- [ ] **Step 4: Register DI**

In `Program.cs`, find the existing billing service registrations (search for `AddScoped<IPlanCatalogService`). Add alongside them:

```csharp
builder.Services.AddScoped<IBillingMetricsService, EfBillingMetricsService>();
```

- [ ] **Step 5: Add the `GET /api/platform/metrics` handler**

In `Program.cs`, directly after the `GET /api/platform/invoices` handler (Task 4 Step 3), insert:

```csharp
app.MapGet("/api/platform/metrics", async (
    PlatformAdminAuthorizationService authorizationService,
    IBillingMetricsService metricsService,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
    {
        await WritePlatformAuditAsync(
            auditRecordWriter,
            organizationId: Guid.Empty,
            actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
            action: AuditActionNames.ViewBilling,
            targetType: "BillingMetrics",
            targetId: null,
            outcome: AuditOutcome.Denied,
            details: new { authorization.DenialReason },
            cancellationToken);
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    }

    var metrics = await metricsService.GetAsync(cancellationToken);
    return Results.Ok(metrics);
});
```

- [ ] **Step 6: Run**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~BillingMetricsTests`
Expected: 3 passing.

- [ ] **Step 7: Full backend gate + commit**

Run: `dotnet build` then `dotnet test`
Expected: Build 0 errors; all tests pass (baseline 586 + the new ones).

```bash
git add src/AFK4.Platform.Api/Platform/Billing/IBillingMetricsService.cs src/AFK4.Platform.Api/Platform/Billing/EfBillingMetricsService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Platform/BillingMetricsTests.cs
git commit -m "feat(platform): billing metrics service + GET /api/platform/metrics"
```

---

## Phase B — Frontend API layer

### Task 6: Billing TypeScript types + name constants

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/types.ts` (append at end of file)

- [ ] **Step 1: Append billing types**

Add to the end of `types.ts`:

```ts
// --- SaaS billing (SP3 Plan 4) ---
export const BillingInterval = {
  Monthly: 'monthly',
  Yearly: 'yearly'
} as const;

export const InvoiceStatus = {
  Issued: 'issued',
  Paid: 'paid',
  Void: 'void',
  Overdue: 'overdue'
} as const;

export const InvoiceKind = {
  Subscription: 'subscription',
  Proration: 'proration'
} as const;

export interface SubscriptionPlan {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export interface CreatePlanRequest {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  sortOrder: number;
}

export interface UpdatePlanRequest {
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export interface TenantSubscription {
  tenantSubscriptionId: string;
  organizationId: string;
  planCode: string;
  status: string;
  currentPeriodStartUtc: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  amountMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  cancelAtPeriodEnd: boolean;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpdateSubscriptionRequest {
  planCode: string | null;
  billingInterval: string | null;
  status: string | null;
  cancelAtPeriodEnd: boolean | null;
}

export interface Invoice {
  invoiceId: string;
  organizationId: string;
  number: number;
  kind: string;
  periodStartUtc: string;
  periodEndUtc: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
  paidAtUtc: string | null;
  voidedAtUtc: string | null;
  voidReason: string | null;
  description: string;
}

export interface SubscriptionListItem {
  tenantSubscriptionId: string;
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  planCode: string;
  status: string;
  billingInterval: string;
  amountMinorUnits: number;
  currencyCode: string;
  currentPeriodEndUtc: string;
  nextInvoiceUtc: string | null;
  cancelAtPeriodEnd: boolean;
}

export interface InvoiceListItem {
  invoiceId: string;
  organizationId: string;
  organizationName: string;
  organizationSlug: string;
  number: number;
  kind: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

export interface PlatformBillingMetrics {
  mrrMinorUnits: number;
  currencyCode: string;
  activeSubscriptions: number;
  outstandingMinorUnits: number;
  outstandingCount: number;
  overdueMinorUnits: number;
  overdueCount: number;
}
```

- [ ] **Step 2: Type-check + commit**

Run (from `src/AFK4.Platform.Web`): `npm run build`
Expected: `tsc -b` + `vite build` succeed.

```bash
git add src/AFK4.Platform.Web/src/api/types.ts
git commit -m "feat(platform-web): billing API types"
```

---

### Task 7: `PlatformApiClient` billing methods + Idempotency-Key support

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/platformApi.ts`

The write actions (`generate`, `mark-paid`, `void`) must send an `Idempotency-Key` header — the backend reads it (Program.cs ~1814) but the client has never sent one. Add a `sendIdempotent` helper that generates a UUID via `crypto.randomUUID()` and a `dispatch` overload accepting extra headers.

- [ ] **Step 1: Extend the imports**

In `platformApi.ts`, extend the `import type { ... } from './types';` block to also import:

```ts
  CreatePlanRequest,
  Invoice,
  InvoiceListItem,
  PlatformBillingMetrics,
  SubscriptionListItem,
  SubscriptionPlan,
  TenantSubscription,
  UpdatePlanRequest,
  UpdateSubscriptionRequest,
```

(Keep the existing imports; merge alphabetically into the same `import type` group.)

- [ ] **Step 2: Add the public billing methods**

Insert these methods into the `PlatformApiClient` class, after `getHealth` (line ~188) and before `private async send`:

```ts
  public listPlans(includeInactive = true): Promise<SubscriptionPlan[]> {
    return this.send<SubscriptionPlan[]>('GET', `/api/platform/plans?includeInactive=${includeInactive ? 'true' : 'false'}`);
  }

  public createPlan(request: CreatePlanRequest): Promise<SubscriptionPlan> {
    return this.send<SubscriptionPlan>('POST', '/api/platform/plans', request);
  }

  public updatePlanCatalog(planCode: string, request: UpdatePlanRequest): Promise<SubscriptionPlan> {
    return this.send<SubscriptionPlan>('PATCH', `/api/platform/plans/${encodeURIComponent(planCode)}`, request);
  }

  public getSubscription(organizationId: string): Promise<TenantSubscription> {
    return this.send<TenantSubscription>('GET', `/api/platform/tenants/${organizationId}/subscription`);
  }

  public updateSubscription(organizationId: string, request: UpdateSubscriptionRequest): Promise<TenantSubscription> {
    return this.send<TenantSubscription>('PATCH', `/api/platform/tenants/${organizationId}/subscription`, request);
  }

  public listTenantInvoices(organizationId: string, status?: string): Promise<Invoice[]> {
    const query = status !== undefined && status.length > 0 ? `?status=${encodeURIComponent(status)}` : '';
    return this.send<Invoice[]>('GET', `/api/platform/tenants/${organizationId}/invoices${query}`);
  }

  public listSubscriptions(status?: string, planCode?: string): Promise<SubscriptionListItem[]> {
    const params = new URLSearchParams();
    if (status !== undefined && status.length > 0) params.set('status', status);
    if (planCode !== undefined && planCode.length > 0) params.set('planCode', planCode);
    const query = params.toString().length > 0 ? `?${params.toString()}` : '';
    return this.send<SubscriptionListItem[]>('GET', `/api/platform/subscriptions${query}`);
  }

  public listInvoices(status?: string): Promise<InvoiceListItem[]> {
    const query = status !== undefined && status.length > 0 ? `?status=${encodeURIComponent(status)}` : '';
    return this.send<InvoiceListItem[]>('GET', `/api/platform/invoices${query}`);
  }

  public getBillingMetrics(): Promise<PlatformBillingMetrics> {
    return this.send<PlatformBillingMetrics>('GET', '/api/platform/metrics');
  }

  public generateInvoice(organizationId: string): Promise<Invoice> {
    return this.sendIdempotent<Invoice>('POST', `/api/platform/tenants/${organizationId}/invoices/generate`, undefined);
  }

  public markInvoicePaid(invoiceId: string, reference: string | null): Promise<Invoice> {
    return this.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/mark-paid`, { reference });
  }

  public voidInvoice(invoiceId: string, reason: string): Promise<Invoice> {
    return this.sendIdempotent<Invoice>('POST', `/api/platform/invoices/${invoiceId}/void`, { reason });
  }
```

- [ ] **Step 3: Add the `sendIdempotent` helper + extend `dispatch`**

Replace the existing `dispatch` method (lines ~208-217) so it accepts an optional `extraHeaders` argument, and add `sendIdempotent` right after `send`:

```ts
  private async sendIdempotent<T>(method: string, path: string, body: unknown | undefined): Promise<T> {
    const idempotencyKey = crypto.randomUUID();
    let response = await this.dispatch(method, path, body, true, { 'Idempotency-Key': idempotencyKey });
    if (response.status === 401 && this.session !== null) {
      const refreshed = await this.refreshTokenOnce();
      if (refreshed !== null) {
        response = await this.dispatch(method, path, body, true, { 'Idempotency-Key': idempotencyKey });
      }
    }
    if (!response.ok) {
      throw await PlatformApiClient.toError(response);
    }
    if (response.status === 204) {
      return undefined as unknown as T;
    }
    const text = await response.text();
    return text.length === 0 ? (undefined as unknown as T) : (JSON.parse(text) as T);
  }

  private dispatch(
    method: string,
    path: string,
    body: unknown | undefined,
    includeAuth: boolean,
    extraHeaders?: Record<string, string>
  ): Promise<Response> {
    const headers = this.buildHeaders(includeAuth && body !== undefined);
    if (extraHeaders !== undefined) {
      for (const [k, v] of Object.entries(extraHeaders)) {
        headers[k] = v;
      }
    }
    const init: RequestInit = { method, headers };
    if (body !== undefined) {
      init.body = JSON.stringify(body);
    }
    return this.fetchImpl(`${this.baseUrl}${path}`, init);
  }
```

> NOTE: `generateInvoice` passes `body: undefined`, so its request has no `Content-Type`/body but still carries the `Idempotency-Key` header — matching the backend (which reads the header and hashes `{ organizationId }`).

- [ ] **Step 4: Type-check + commit**

Run (from `src/AFK4.Platform.Web`): `npm run build`
Expected: success.

```bash
git add src/AFK4.Platform.Web/src/api/platformApi.ts
git commit -m "feat(platform-web): billing client methods + idempotency-key support"
```

---

## Phase C — Billing module (model, hooks)

### Task 8: `billingModel.ts` — pure helpers

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/billingModel.ts`
- Test: `src/AFK4.Platform.Web/src/platform/billing/billingModel.test.ts`

- [ ] **Step 1: Write the failing test**

```ts
import { describe, expect, it } from 'vitest';
import {
  filterInvoices,
  filterSubscriptions,
  validatePlanForm,
  INVOICE_STATUS_VARIANT,
  SUBSCRIPTION_STATUS_VARIANT,
  emptyPlanForm,
  planFormToCreateRequest
} from './billingModel';
import type { InvoiceListItem, SubscriptionListItem } from '@/api/types';

function sub(p: Partial<SubscriptionListItem>): SubscriptionListItem {
  return {
    tenantSubscriptionId: 't', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    planCode: 'starter', status: 'active', billingInterval: 'monthly', amountMinorUnits: 290000,
    currencyCode: 'RUB', currentPeriodEndUtc: '2026-06-30T00:00:00Z', nextInvoiceUtc: null,
    cancelAtPeriodEnd: false, ...p
  };
}

function inv(p: Partial<InvoiceListItem>): InvoiceListItem {
  return {
    invoiceId: 'i', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    number: 1, kind: 'subscription', issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
    amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued', ...p
  };
}

describe('filterSubscriptions', () => {
  it('filters by status and query (name/slug)', () => {
    const rows = [
      sub({ organizationSlug: 'acme', organizationName: 'Acme', status: 'active' }),
      sub({ organizationSlug: 'beta', organizationName: 'Beta', status: 'cancelled' })
    ];
    expect(filterSubscriptions(rows, { query: '', status: 'all' })).toHaveLength(2);
    expect(filterSubscriptions(rows, { query: '', status: 'cancelled' })).toHaveLength(1);
    expect(filterSubscriptions(rows, { query: 'acm', status: 'all' })).toHaveLength(1);
  });
});

describe('filterInvoices', () => {
  it('filters by status and query', () => {
    const rows = [
      inv({ organizationSlug: 'acme', status: 'issued' }),
      inv({ organizationSlug: 'beta', status: 'paid' })
    ];
    expect(filterInvoices(rows, { query: '', status: 'all' })).toHaveLength(2);
    expect(filterInvoices(rows, { query: '', status: 'paid' })).toHaveLength(1);
    expect(filterInvoices(rows, { query: 'bet', status: 'all' })).toHaveLength(1);
  });
});

describe('status variant maps', () => {
  it('maps each known status to a badge variant', () => {
    expect(INVOICE_STATUS_VARIANT.paid).toBeDefined();
    expect(INVOICE_STATUS_VARIANT.overdue).toBe('destructive');
    expect(SUBSCRIPTION_STATUS_VARIANT.active).toBeDefined();
  });
});

describe('validatePlanForm', () => {
  it('rejects blank code/name and negative price', () => {
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: '', name: 'X' })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: '' })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: 'X', priceMinorUnits: -1 })).toBe(false);
    expect(validatePlanForm({ ...emptyPlanForm(), planCode: 'x', name: 'X', priceMinorUnits: 0 })).toBe(true);
  });

  it('converts a form to a create request', () => {
    const req = planFormToCreateRequest({ ...emptyPlanForm(), planCode: 'pro', name: 'Pro', priceMinorUnits: 100 });
    expect(req.planCode).toBe('pro');
    expect(req.priceMinorUnits).toBe(100);
    expect(req.billingInterval).toBe('monthly');
  });
});
```

- [ ] **Step 2: Run to confirm failure**

Run: `npm test -- billingModel`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `billingModel.ts`**

```ts
import type {
  CreatePlanRequest,
  InvoiceListItem,
  SubscriptionListItem,
  SubscriptionPlan,
  UpdatePlanRequest
} from '@/api/types';
import type { MessageKey } from '@/i18n/messages';
import type { BadgeVariant } from '@/components/ui/badge';

export interface ListFilter {
  query: string;
  status: string; // 'all' | concrete status
}

export function filterSubscriptions(rows: SubscriptionListItem[], filter: ListFilter): SubscriptionListItem[] {
  const q = filter.query.trim().toLowerCase();
  return rows
    .filter(r => filter.status === 'all' || r.status === filter.status)
    .filter(r => q === '' || r.organizationName.toLowerCase().includes(q) || r.organizationSlug.toLowerCase().includes(q));
}

export function filterInvoices(rows: InvoiceListItem[], filter: ListFilter): InvoiceListItem[] {
  const q = filter.query.trim().toLowerCase();
  return rows
    .filter(r => filter.status === 'all' || r.status === filter.status)
    .filter(r => q === '' || r.organizationName.toLowerCase().includes(q) || r.organizationSlug.toLowerCase().includes(q));
}

export const INVOICE_STATUS_VARIANT: Record<string, BadgeVariant> = {
  issued: 'secondary',
  paid: 'success',
  void: 'outline',
  overdue: 'destructive'
};

export const INVOICE_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'platform.billing.invoiceStatus.issued',
  paid: 'platform.billing.invoiceStatus.paid',
  void: 'platform.billing.invoiceStatus.void',
  overdue: 'platform.billing.invoiceStatus.overdue'
};

export const INVOICE_KIND_LABEL: Record<string, MessageKey> = {
  subscription: 'platform.billing.invoiceKind.subscription',
  proration: 'platform.billing.invoiceKind.proration'
};

export const SUBSCRIPTION_STATUS_VARIANT: Record<string, BadgeVariant> = {
  trial: 'secondary',
  active: 'success',
  past_due: 'destructive',
  cancelled: 'outline'
};

export const SUBSCRIPTION_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'platform.tenant.subscription.trial',
  active: 'platform.tenant.subscription.active',
  past_due: 'platform.tenant.subscription.pastDue',
  cancelled: 'platform.tenant.subscription.cancelled'
};

export const INTERVAL_LABEL: Record<string, MessageKey> = {
  monthly: 'platform.billing.interval.monthly',
  yearly: 'platform.billing.interval.yearly'
};

export const INVOICE_STATUS_FILTERS = ['all', 'issued', 'paid', 'void', 'overdue'] as const;
export const SUBSCRIPTION_STATUS_FILTERS = ['all', 'trial', 'active', 'past_due', 'cancelled'] as const;

export interface PlanForm {
  planCode: string;
  name: string;
  priceMinorUnits: number;
  currencyCode: string;
  billingInterval: string;
  maxBranches: number | null;
  maxDevicesPerBranch: number | null;
  maxConcurrentSessions: number | null;
  maxStaffUsersPerBranch: number | null;
  isActive: boolean;
  sortOrder: number;
}

export function emptyPlanForm(): PlanForm {
  return {
    planCode: '',
    name: '',
    priceMinorUnits: 0,
    currencyCode: 'RUB',
    billingInterval: 'monthly',
    maxBranches: null,
    maxDevicesPerBranch: null,
    maxConcurrentSessions: null,
    maxStaffUsersPerBranch: null,
    isActive: true,
    sortOrder: 0
  };
}

export function planToForm(plan: SubscriptionPlan): PlanForm {
  return {
    planCode: plan.planCode,
    name: plan.name,
    priceMinorUnits: plan.priceMinorUnits,
    currencyCode: plan.currencyCode,
    billingInterval: plan.billingInterval,
    maxBranches: plan.maxBranches,
    maxDevicesPerBranch: plan.maxDevicesPerBranch,
    maxConcurrentSessions: plan.maxConcurrentSessions,
    maxStaffUsersPerBranch: plan.maxStaffUsersPerBranch,
    isActive: plan.isActive,
    sortOrder: plan.sortOrder
  };
}

export function validatePlanForm(form: PlanForm): boolean {
  if (form.planCode.trim() === '') return false;
  if (form.name.trim() === '') return false;
  if (!Number.isFinite(form.priceMinorUnits) || form.priceMinorUnits < 0) return false;
  return true;
}

export function planFormToCreateRequest(form: PlanForm): CreatePlanRequest {
  return {
    planCode: form.planCode.trim(),
    name: form.name.trim(),
    priceMinorUnits: form.priceMinorUnits,
    currencyCode: form.currencyCode,
    billingInterval: form.billingInterval,
    maxBranches: form.maxBranches,
    maxDevicesPerBranch: form.maxDevicesPerBranch,
    maxConcurrentSessions: form.maxConcurrentSessions,
    maxStaffUsersPerBranch: form.maxStaffUsersPerBranch,
    sortOrder: form.sortOrder
  };
}

export function planFormToUpdateRequest(form: PlanForm): UpdatePlanRequest {
  return {
    name: form.name.trim(),
    priceMinorUnits: form.priceMinorUnits,
    currencyCode: form.currencyCode,
    billingInterval: form.billingInterval,
    maxBranches: form.maxBranches,
    maxDevicesPerBranch: form.maxDevicesPerBranch,
    maxConcurrentSessions: form.maxConcurrentSessions,
    maxStaffUsersPerBranch: form.maxStaffUsersPerBranch,
    isActive: form.isActive,
    sortOrder: form.sortOrder
  };
}
```

> NOTE: Confirm `BadgeVariant` includes `'success'`, `'secondary'`, `'destructive'`, `'outline'` by checking `src/components/ui/badge.tsx` (the tenants module already uses `'success'`/`'destructive'`/`'outline'`). If `'secondary'` is not a valid variant, substitute the existing neutral variant used elsewhere (e.g. `'outline'`).

- [ ] **Step 4: Run + commit**

Run: `npm test -- billingModel` (expect pass), then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/billingModel.ts src/AFK4.Platform.Web/src/platform/billing/billingModel.test.ts
git commit -m "feat(platform-web): billing view-model helpers"
```

---

### Task 9: Data hooks (`usePlans`, `useSubscriptions`, `useInvoices`, `useBillingMetrics`)

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/usePlans.ts` (+ `.test.tsx`)
- Create: `src/AFK4.Platform.Web/src/platform/billing/useSubscriptions.ts`
- Create: `src/AFK4.Platform.Web/src/platform/billing/useInvoices.ts`
- Create: `src/AFK4.Platform.Web/src/platform/billing/useBillingMetrics.ts` (+ `.test.tsx`)

All four follow the exact discriminated-union pattern of `useTenants.ts` (loading|error|ready + `retry` + `useRef(client)` + `[tick]` deps). Plans/invoices hooks reload on a filter argument too.

- [ ] **Step 1: Write `usePlans.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionPlan } from '@/api/types';

export type PlansState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SubscriptionPlan[]; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listPlans'>;

export function usePlans(client: Loadable): PlansState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SubscriptionPlan[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listPlans(true)
      .then(plans => { if (!cancelled) setState({ status: 'ready', data: plans }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 2: Write `useSubscriptions.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionListItem } from '@/api/types';

export type SubscriptionsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: SubscriptionListItem[]; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listSubscriptions'>;

export function useSubscriptions(client: Loadable): SubscriptionsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: SubscriptionListItem[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listSubscriptions()
      .then(rows => { if (!cancelled) setState({ status: 'ready', data: rows }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 3: Write `useInvoices.ts`** (cross-tenant; same shape, calls `listInvoices`)

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { InvoiceListItem } from '@/api/types';

export type InvoicesState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: InvoiceListItem[]; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'listInvoices'>;

export function useInvoices(client: Loadable): InvoicesState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: InvoiceListItem[]; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.listInvoices()
      .then(rows => { if (!cancelled) setState({ status: 'ready', data: rows }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 4: Write `useBillingMetrics.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { PlatformApiClient } from '@/api/platformApi';
import type { PlatformBillingMetrics } from '@/api/types';

export type BillingMetricsState =
  | { status: 'loading'; retry: () => void }
  | { status: 'error'; message: string; retry: () => void }
  | { status: 'ready'; data: PlatformBillingMetrics; retry: () => void };

type Loadable = Pick<PlatformApiClient, 'getBillingMetrics'>;

export function useBillingMetrics(client: Loadable): BillingMetricsState {
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<{ status: 'loading' | 'error' | 'ready'; data?: PlatformBillingMetrics; message?: string }>({ status: 'loading' });
  const retry = useCallback(() => setTick(t => t + 1), []);
  const clientRef = useRef(client);
  clientRef.current = client;

  useEffect(() => {
    let cancelled = false;
    setState({ status: 'loading' });
    clientRef.current.getBillingMetrics()
      .then(m => { if (!cancelled) setState({ status: 'ready', data: m }); })
      .catch((err: unknown) => { if (!cancelled) setState({ status: 'error', message: err instanceof Error ? err.message : 'error' }); });
    return () => { cancelled = true; };
  }, [tick]);

  if (state.status === 'ready') return { status: 'ready', data: state.data!, retry };
  if (state.status === 'error') return { status: 'error', message: state.message ?? 'error', retry };
  return { status: 'loading', retry };
}
```

- [ ] **Step 5: Write `usePlans.test.tsx` (representative hook test)**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { usePlans } from './usePlans';

function fakeClient(over: Partial<Record<'listPlans', unknown>> = {}) {
  return { listPlans: vi.fn().mockResolvedValue([]), ...over } as never;
}

describe('usePlans', () => {
  it('reaches ready', async () => {
    const { result } = renderHook(() => usePlans(fakeClient()));
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });

  it('errors then retry reloads', async () => {
    const client = fakeClient({ listPlans: vi.fn().mockRejectedValueOnce(new Error('boom')).mockResolvedValue([]) });
    const { result } = renderHook(() => usePlans(client));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 6: Write `useBillingMetrics.test.tsx`**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { useBillingMetrics } from './useBillingMetrics';

const metrics = {
  mrrMinorUnits: 580000, currencyCode: 'RUB', activeSubscriptions: 2,
  outstandingMinorUnits: 290000, outstandingCount: 1, overdueMinorUnits: 0, overdueCount: 0
};

describe('useBillingMetrics', () => {
  it('reaches ready with metrics', async () => {
    const client = { getBillingMetrics: vi.fn().mockResolvedValue(metrics) } as never;
    const { result } = renderHook(() => useBillingMetrics(client));
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') expect(result.current.data.mrrMinorUnits).toBe(580000);
  });
});
```

- [ ] **Step 7: Run + commit**

Run: `npm test -- usePlans useBillingMetrics` (expect pass), then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/usePlans.ts src/AFK4.Platform.Web/src/platform/billing/useSubscriptions.ts src/AFK4.Platform.Web/src/platform/billing/useInvoices.ts src/AFK4.Platform.Web/src/platform/billing/useBillingMetrics.ts src/AFK4.Platform.Web/src/platform/billing/usePlans.test.tsx src/AFK4.Platform.Web/src/platform/billing/useBillingMetrics.test.tsx
git commit -m "feat(platform-web): billing data hooks"
```

---

## Phase D — i18n keys

### Task 10: Add `platform.billing.*` + tenant-section keys

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts`

Add the SAME keys to BOTH the `ru` block and the `en` block (parity is test-enforced). Place them near the existing `platform.tenant.*` / `platform.overview.*` keys.

- [ ] **Step 1: Add keys to the `ru` block**

Insert into the `ru` object (after the existing `platform.tenant.*` keys):

```ts
    'nav.platform.billing': 'Биллинг',

    'platform.billing.tab.subscriptions': 'Подписки',
    'platform.billing.tab.invoices': 'Инвойсы',
    'platform.billing.tab.plans': 'Тарифы',
    'platform.billing.filter.allStatuses': 'Все статусы',
    'platform.billing.search.placeholder': 'Поиск по тенанту',
    'platform.billing.column.tenant': 'Тенант',
    'platform.billing.column.plan': 'Тариф',
    'platform.billing.column.status': 'Статус',
    'platform.billing.column.amount': 'Сумма',
    'platform.billing.column.interval': 'Период',
    'platform.billing.column.periodEnd': 'Конец периода',
    'platform.billing.column.number': '№',
    'platform.billing.column.issued': 'Выставлен',
    'platform.billing.column.due': 'Срок',
    'platform.billing.column.kind': 'Тип',
    'platform.billing.column.actions': 'Действия',
    'platform.billing.empty.subscriptions': 'Подписок пока нет.',
    'platform.billing.empty.invoices': 'Инвойсов пока нет.',
    'platform.billing.empty.plans': 'Тарифов пока нет.',
    'platform.billing.interval.monthly': 'Ежемесячно',
    'platform.billing.interval.yearly': 'Ежегодно',
    'platform.billing.invoiceStatus.issued': 'Выставлен',
    'platform.billing.invoiceStatus.paid': 'Оплачен',
    'platform.billing.invoiceStatus.void': 'Аннулирован',
    'platform.billing.invoiceStatus.overdue': 'Просрочен',
    'platform.billing.invoiceKind.subscription': 'Подписка',
    'platform.billing.invoiceKind.proration': 'Перерасчёт',
    'platform.billing.action.markPaid': 'Отметить оплаченным',
    'platform.billing.action.void': 'Аннулировать',
    'platform.billing.action.generate': 'Сгенерировать инвойс',
    'platform.billing.markPaid.title': 'Отметить инвойс оплаченным?',
    'platform.billing.markPaid.reference': 'Референс платежа (необязательно)',
    'platform.billing.markPaid.confirm': 'Отметить оплаченным',
    'platform.billing.markPaid.done': 'Инвойс отмечен оплаченным',
    'platform.billing.void.title': 'Аннулировать инвойс?',
    'platform.billing.void.reason': 'Причина',
    'platform.billing.void.confirm': 'Аннулировать',
    'platform.billing.void.done': 'Инвойс аннулирован',
    'platform.billing.generate.done': 'Инвойс создан',
    'platform.billing.generate.none': 'Нет инвойса к созданию для текущего периода',
    'platform.billing.action.cancel': 'Отмена',
    'platform.billing.action.error': 'Не удалось выполнить операцию',
    'platform.billing.plans.create': 'Новый тариф',
    'platform.billing.plans.edit': 'Изменить',
    'platform.billing.plans.column.price': 'Цена',
    'platform.billing.plans.column.limits': 'Лимиты',
    'platform.billing.plans.column.active': 'Активен',
    'platform.billing.planForm.createTitle': 'Новый тариф',
    'platform.billing.planForm.editTitle': 'Изменить тариф',
    'platform.billing.planForm.code': 'Код тарифа',
    'platform.billing.planForm.name': 'Название',
    'platform.billing.planForm.price': 'Цена (в минорных единицах)',
    'platform.billing.planForm.currency': 'Валюта',
    'platform.billing.planForm.interval': 'Период оплаты',
    'platform.billing.planForm.maxBranches': 'Макс. филиалов',
    'platform.billing.planForm.maxDevices': 'Макс. устройств на филиал',
    'platform.billing.planForm.maxSessions': 'Макс. одновременных сессий',
    'platform.billing.planForm.maxStaff': 'Макс. сотрудников на филиал',
    'platform.billing.planForm.sortOrder': 'Порядок',
    'platform.billing.planForm.active': 'Активен',
    'platform.billing.planForm.save': 'Сохранить',
    'platform.billing.planForm.created': 'Тариф создан',
    'platform.billing.planForm.updated': 'Тариф обновлён',

    'platform.tenant.section.subscription': 'Подписка',
    'platform.tenant.section.invoices': 'Инвойсы',
    'platform.tenant.subscriptionForm.plan': 'Тариф',
    'platform.tenant.subscriptionForm.interval': 'Период оплаты',
    'platform.tenant.subscriptionForm.status': 'Статус',
    'platform.tenant.subscriptionForm.cancelAtPeriodEnd': 'Отменить в конце периода',
    'platform.tenant.subscriptionForm.currentPeriod': 'Текущий период',
    'platform.tenant.subscriptionForm.nextInvoice': 'Следующий инвойс',
    'platform.tenant.subscriptionForm.amount': 'Сумма',
    'platform.tenant.subscriptionForm.apply': 'Сохранить',
    'platform.tenant.subscriptionForm.updated': 'Подписка обновлена',
    'platform.tenant.invoices.empty': 'Инвойсов пока нет.',
    'platform.tenant.invoices.generate': 'Сгенерировать инвойс',

    'platform.overview.kpi.mrr': 'MRR',
    'platform.overview.kpi.outstanding': 'К оплате',
    'platform.overview.kpi.overdue': 'Просрочено',
```

> NOTE: If `nav.platform.billing` already exists in the `ru`/`en` blocks (Plan 1 added it), do NOT duplicate it — TypeScript object literals reject duplicate keys. Remove the `nav.platform.billing` line from this insert in that case.

- [ ] **Step 2: Add the identical key set to the `en` block** with English values:

```ts
    'platform.billing.tab.subscriptions': 'Subscriptions',
    'platform.billing.tab.invoices': 'Invoices',
    'platform.billing.tab.plans': 'Plans',
    'platform.billing.filter.allStatuses': 'All statuses',
    'platform.billing.search.placeholder': 'Search by tenant',
    'platform.billing.column.tenant': 'Tenant',
    'platform.billing.column.plan': 'Plan',
    'platform.billing.column.status': 'Status',
    'platform.billing.column.amount': 'Amount',
    'platform.billing.column.interval': 'Interval',
    'platform.billing.column.periodEnd': 'Period end',
    'platform.billing.column.number': 'No.',
    'platform.billing.column.issued': 'Issued',
    'platform.billing.column.due': 'Due',
    'platform.billing.column.kind': 'Kind',
    'platform.billing.column.actions': 'Actions',
    'platform.billing.empty.subscriptions': 'No subscriptions yet.',
    'platform.billing.empty.invoices': 'No invoices yet.',
    'platform.billing.empty.plans': 'No plans yet.',
    'platform.billing.interval.monthly': 'Monthly',
    'platform.billing.interval.yearly': 'Yearly',
    'platform.billing.invoiceStatus.issued': 'Issued',
    'platform.billing.invoiceStatus.paid': 'Paid',
    'platform.billing.invoiceStatus.void': 'Void',
    'platform.billing.invoiceStatus.overdue': 'Overdue',
    'platform.billing.invoiceKind.subscription': 'Subscription',
    'platform.billing.invoiceKind.proration': 'Proration',
    'platform.billing.action.markPaid': 'Mark paid',
    'platform.billing.action.void': 'Void',
    'platform.billing.action.generate': 'Generate invoice',
    'platform.billing.markPaid.title': 'Mark invoice as paid?',
    'platform.billing.markPaid.reference': 'Payment reference (optional)',
    'platform.billing.markPaid.confirm': 'Mark paid',
    'platform.billing.markPaid.done': 'Invoice marked paid',
    'platform.billing.void.title': 'Void invoice?',
    'platform.billing.void.reason': 'Reason',
    'platform.billing.void.confirm': 'Void',
    'platform.billing.void.done': 'Invoice voided',
    'platform.billing.generate.done': 'Invoice created',
    'platform.billing.generate.none': 'No invoice due for the current period',
    'platform.billing.action.cancel': 'Cancel',
    'platform.billing.action.error': 'The operation failed',
    'platform.billing.plans.create': 'New plan',
    'platform.billing.plans.edit': 'Edit',
    'platform.billing.plans.column.price': 'Price',
    'platform.billing.plans.column.limits': 'Limits',
    'platform.billing.plans.column.active': 'Active',
    'platform.billing.planForm.createTitle': 'New plan',
    'platform.billing.planForm.editTitle': 'Edit plan',
    'platform.billing.planForm.code': 'Plan code',
    'platform.billing.planForm.name': 'Name',
    'platform.billing.planForm.price': 'Price (minor units)',
    'platform.billing.planForm.currency': 'Currency',
    'platform.billing.planForm.interval': 'Billing interval',
    'platform.billing.planForm.maxBranches': 'Max branches',
    'platform.billing.planForm.maxDevices': 'Max devices per branch',
    'platform.billing.planForm.maxSessions': 'Max concurrent sessions',
    'platform.billing.planForm.maxStaff': 'Max staff per branch',
    'platform.billing.planForm.sortOrder': 'Sort order',
    'platform.billing.planForm.active': 'Active',
    'platform.billing.planForm.save': 'Save',
    'platform.billing.planForm.created': 'Plan created',
    'platform.billing.planForm.updated': 'Plan updated',

    'platform.tenant.section.subscription': 'Subscription',
    'platform.tenant.section.invoices': 'Invoices',
    'platform.tenant.subscriptionForm.plan': 'Plan',
    'platform.tenant.subscriptionForm.interval': 'Billing interval',
    'platform.tenant.subscriptionForm.status': 'Status',
    'platform.tenant.subscriptionForm.cancelAtPeriodEnd': 'Cancel at period end',
    'platform.tenant.subscriptionForm.currentPeriod': 'Current period',
    'platform.tenant.subscriptionForm.nextInvoice': 'Next invoice',
    'platform.tenant.subscriptionForm.amount': 'Amount',
    'platform.tenant.subscriptionForm.apply': 'Save',
    'platform.tenant.subscriptionForm.updated': 'Subscription updated',
    'platform.tenant.invoices.empty': 'No invoices yet.',
    'platform.tenant.invoices.generate': 'Generate invoice',

    'platform.overview.kpi.mrr': 'MRR',
    'platform.overview.kpi.outstanding': 'Outstanding',
    'platform.overview.kpi.overdue': 'Overdue',
```

- [ ] **Step 3: Run parity test + build**

Run: `npm test -- messages` (expect ru/en parity pass), then `npm run build`.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Web/src/i18n/messages.ts
git commit -m "feat(platform-web): billing i18n keys (ru/en)"
```

---

## Phase E — Billing screen (tabs)

### Task 11: `PlanFormDialog` + `PlansTab`

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/PlanFormDialog.tsx` (+ `.test.tsx`)
- Create: `src/AFK4.Platform.Web/src/platform/billing/PlansTab.tsx`

- [ ] **Step 1: Write `PlanFormDialog.tsx`**

```tsx
import { useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { useI18n } from '@/i18n/I18nProvider';
import { validatePlanForm, type PlanForm } from './billingModel';

interface Props {
  open: boolean;
  mode: 'create' | 'edit';
  form: PlanForm;
  pending: boolean;
  onChange: (form: PlanForm) => void;
  onSubmit: () => void;
  onOpenChange: (open: boolean) => void;
}

export function PlanFormDialog({ open, mode, form, pending, onChange, onSubmit, onOpenChange }: Props) {
  const { t } = useI18n();
  const valid = validatePlanForm(form);

  const numberField = (label: string, value: number | null, set: (n: number | null) => void) => (
    <label className="block text-sm">
      <span className="mb-1 block text-muted-foreground">{label}</span>
      <Input
        type="number"
        value={value === null ? '' : String(value)}
        onChange={e => set(e.target.value === '' ? null : Math.max(0, Math.trunc(Number(e.target.value))))}
      />
    </label>
  );

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogTitle>{mode === 'create' ? t('platform.billing.planForm.createTitle') : t('platform.billing.planForm.editTitle')}</DialogTitle>
        <div className="flex max-h-[60vh] flex-col gap-3 overflow-y-auto">
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.code')}</span>
            <Input value={form.planCode} disabled={mode === 'edit'} onChange={e => onChange({ ...form, planCode: e.target.value })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.name')}</span>
            <Input value={form.name} onChange={e => onChange({ ...form, name: e.target.value })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.price')}</span>
            <Input type="number" value={String(form.priceMinorUnits)} onChange={e => onChange({ ...form, priceMinorUnits: Math.max(0, Math.trunc(Number(e.target.value))) })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.currency')}</span>
            <Input value={form.currencyCode} onChange={e => onChange({ ...form, currencyCode: e.target.value.toUpperCase() })} />
          </label>
          <label className="block text-sm">
            <span className="mb-1 block text-muted-foreground">{t('platform.billing.planForm.interval')}</span>
            <Select value={form.billingInterval} onValueChange={v => onChange({ ...form, billingInterval: v })}>
              <SelectTrigger aria-label={t('platform.billing.planForm.interval')}><SelectValue /></SelectTrigger>
              <SelectContent>
                <SelectItem value="monthly">{t('platform.billing.interval.monthly')}</SelectItem>
                <SelectItem value="yearly">{t('platform.billing.interval.yearly')}</SelectItem>
              </SelectContent>
            </Select>
          </label>
          {numberField(t('platform.billing.planForm.maxBranches'), form.maxBranches, n => onChange({ ...form, maxBranches: n }))}
          {numberField(t('platform.billing.planForm.maxDevices'), form.maxDevicesPerBranch, n => onChange({ ...form, maxDevicesPerBranch: n }))}
          {numberField(t('platform.billing.planForm.maxSessions'), form.maxConcurrentSessions, n => onChange({ ...form, maxConcurrentSessions: n }))}
          {numberField(t('platform.billing.planForm.maxStaff'), form.maxStaffUsersPerBranch, n => onChange({ ...form, maxStaffUsersPerBranch: n }))}
          {numberField(t('platform.billing.planForm.sortOrder'), form.sortOrder, n => onChange({ ...form, sortOrder: n ?? 0 }))}
          {mode === 'edit' && (
            <label className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">{t('platform.billing.planForm.active')}</span>
              <Switch checked={form.isActive} onCheckedChange={c => onChange({ ...form, isActive: c })} />
            </label>
          )}
        </div>
        <DialogFooter>
          <Button variant="outline" disabled={pending} onClick={() => onOpenChange(false)}>{t('platform.billing.action.cancel')}</Button>
          <Button disabled={pending || !valid} onClick={onSubmit}>{t('platform.billing.planForm.save')}</Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

> NOTE: Verify `Switch`'s callback prop name in `src/components/ui/switch.tsx` (`onCheckedChange` is the shadcn convention; `TenantLimitsSection`/settings screens already use it — match whatever they use).

- [ ] **Step 2: Write `PlansTab.tsx`**

```tsx
import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionPlan } from '@/api/types';
import { usePlans } from './usePlans';
import { PlanFormDialog } from './PlanFormDialog';
import { emptyPlanForm, planToForm, planFormToCreateRequest, planFormToUpdateRequest, INTERVAL_LABEL, type PlanForm } from './billingModel';

export function PlansTab({ client }: { client: PlatformApiClient }) {
  const { t, formatCurrency } = useI18n();
  const { toast } = useToast();
  const state = usePlans(client);
  const [dialogOpen, setDialogOpen] = useState(false);
  const [mode, setMode] = useState<'create' | 'edit'>('create');
  const [form, setForm] = useState<PlanForm>(emptyPlanForm());
  const [pending, setPending] = useState(false);

  function openCreate() { setMode('create'); setForm(emptyPlanForm()); setDialogOpen(true); }
  function openEdit(plan: SubscriptionPlan) { setMode('edit'); setForm(planToForm(plan)); setDialogOpen(true); }

  async function submit() {
    setPending(true);
    try {
      if (mode === 'create') {
        await client.createPlan(planFormToCreateRequest(form));
        toast({ title: t('platform.billing.planForm.created'), variant: 'success' });
      } else {
        await client.updatePlanCatalog(form.planCode, planFormToUpdateRequest(form));
        toast({ title: t('platform.billing.planForm.updated'), variant: 'success' });
      }
      setDialogOpen(false);
      if (state.status === 'ready') state.retry();
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.billing.tab.plans')}</CardTitle>
        <Button onClick={openCreate}>{t('platform.billing.plans.create')}</Button>
      </CardHeader>
      <CardContent>
        {state.data.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.plans')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.plan')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.price')}</TableHead>
                <TableHead>{t('platform.billing.column.interval')}</TableHead>
                <TableHead>{t('platform.billing.plans.column.active')}</TableHead>
                <TableHead>{t('platform.billing.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {state.data.map(plan => (
                <TableRow key={plan.planCode}>
                  <TableCell><span className="font-medium">{plan.name}</span> <code className="text-xs text-muted-foreground">{plan.planCode}</code></TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(plan.priceMinorUnits, plan.currencyCode)}</TableCell>
                  <TableCell>{INTERVAL_LABEL[plan.billingInterval] ? t(INTERVAL_LABEL[plan.billingInterval]) : plan.billingInterval}</TableCell>
                  <TableCell>{plan.isActive ? <Badge variant="success">●</Badge> : <Badge variant="outline">—</Badge>}</TableCell>
                  <TableCell><Button variant="outline" onClick={() => openEdit(plan)}>{t('platform.billing.plans.edit')}</Button></TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <PlanFormDialog open={dialogOpen} mode={mode} form={form} pending={pending} onChange={setForm} onSubmit={() => void submit()} onOpenChange={setDialogOpen} />
    </Card>
  );
}
```

> NOTE: Confirm `formatCurrency(minorUnits, currencyCode)` signature in `I18nProvider.tsx` and the `Table`/`EmptyState` component import paths/exports against an existing club table screen (e.g. `src/club/clients/*` or `TenantsTable.tsx`). Match the real signatures.

- [ ] **Step 3: Write `PlanFormDialog.test.tsx`**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlanFormDialog } from './PlanFormDialog';
import { emptyPlanForm } from './billingModel';

function renderDialog(over: Partial<Parameters<typeof PlanFormDialog>[0]> = {}) {
  return render(
    <I18nProvider>
      <PlanFormDialog open mode="create" form={emptyPlanForm()} pending={false} onChange={vi.fn()} onSubmit={vi.fn()} onOpenChange={vi.fn()} {...over} />
    </I18nProvider>
  );
}

describe('PlanFormDialog', () => {
  it('renders the create title', () => {
    renderDialog();
    expect(screen.getByText('Новый тариф')).toBeInTheDocument();
  });
});
```

> NOTE: Check how existing screen tests wrap components with `I18nProvider` (default locale ru). If `I18nProvider` needs props, copy them from an existing `*Screen.test.tsx`.

- [ ] **Step 4: Run + commit**

Run: `npm test -- PlanFormDialog` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/PlanFormDialog.tsx src/AFK4.Platform.Web/src/platform/billing/PlansTab.tsx src/AFK4.Platform.Web/src/platform/billing/PlanFormDialog.test.tsx
git commit -m "feat(platform-web): billing plans tab + plan form dialog"
```

---

### Task 12: `SubscriptionsTab`

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/SubscriptionsTab.tsx`

- [ ] **Step 1: Implement `SubscriptionsTab.tsx`**

```tsx
import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { useSubscriptions } from './useSubscriptions';
import {
  filterSubscriptions, SUBSCRIPTION_STATUS_VARIANT, SUBSCRIPTION_STATUS_LABEL,
  INTERVAL_LABEL, SUBSCRIPTION_STATUS_FILTERS
} from './billingModel';

export function SubscriptionsTab({ client }: { client: PlatformApiClient }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const state = useSubscriptions(client);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const rows = filterSubscriptions(state.data, { query, status });

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6">
        <div className="flex flex-wrap gap-2">
          <Input className="max-w-xs" placeholder={t('platform.billing.search.placeholder')} value={query} onChange={e => setQuery(e.target.value)} />
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="max-w-[200px]" aria-label={t('platform.billing.column.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {SUBSCRIPTION_STATUS_FILTERS.map(s => (
                <SelectItem key={s} value={s}>{s === 'all' ? t('platform.billing.filter.allStatuses') : t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {rows.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.subscriptions')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.tenant')}</TableHead>
                <TableHead>{t('platform.billing.column.plan')}</TableHead>
                <TableHead>{t('platform.billing.column.status')}</TableHead>
                <TableHead>{t('platform.billing.column.amount')}</TableHead>
                <TableHead>{t('platform.billing.column.interval')}</TableHead>
                <TableHead>{t('platform.billing.column.periodEnd')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map(r => (
                <TableRow key={r.tenantSubscriptionId}>
                  <TableCell><span className="font-medium">{r.organizationName}</span> <code className="text-xs text-muted-foreground">{r.organizationSlug}</code></TableCell>
                  <TableCell>{r.planCode}</TableCell>
                  <TableCell><Badge variant={SUBSCRIPTION_STATUS_VARIANT[r.status] ?? 'outline'}>{SUBSCRIPTION_STATUS_LABEL[r.status] ? t(SUBSCRIPTION_STATUS_LABEL[r.status]) : r.status}</Badge></TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(r.amountMinorUnits, r.currencyCode)}</TableCell>
                  <TableCell>{INTERVAL_LABEL[r.billingInterval] ? t(INTERVAL_LABEL[r.billingInterval]) : r.billingInterval}</TableCell>
                  <TableCell>{formatDate(r.currentPeriodEndUtc)}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Build + commit**

Run: `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/SubscriptionsTab.tsx
git commit -m "feat(platform-web): billing subscriptions tab"
```

---

### Task 13: `InvoicesTab` (with mark-paid / void via ConfirmDialog)

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/InvoicesTab.tsx` (+ `.test.tsx`)

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { InvoicesTab } from './InvoicesTab';
import type { InvoiceListItem } from '@/api/types';

function invoice(p: Partial<InvoiceListItem>): InvoiceListItem {
  return {
    invoiceId: 'inv-1', organizationId: 'o', organizationName: 'Acme', organizationSlug: 'acme',
    number: 7, kind: 'subscription', issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
    amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued', ...p
  };
}

function fakeClient() {
  return {
    listInvoices: vi.fn().mockResolvedValue([invoice({})]),
    markInvoicePaid: vi.fn().mockResolvedValue(invoice({ status: 'paid' })),
    voidInvoice: vi.fn().mockResolvedValue(invoice({ status: 'void' }))
  } as never;
}

describe('InvoicesTab', () => {
  it('renders invoice rows after load', async () => {
    render(
      <I18nProvider><ToastProvider><InvoicesTab client={fakeClient()} /></ToastProvider></I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Acme')).toBeInTheDocument());
  });
});
```

> NOTE: Verify the toast provider's component name/import (`ToastProvider` from `@/components/ui/toast`) against an existing screen test that triggers toasts; match it. If screens don't need a provider wrapper in tests, drop it.

- [ ] **Step 2: Implement `InvoicesTab.tsx`**

```tsx
import { useState } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { InvoiceListItem } from '@/api/types';
import { useInvoices } from './useInvoices';
import {
  filterInvoices, INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL, INVOICE_KIND_LABEL, INVOICE_STATUS_FILTERS
} from './billingModel';

type Action = { kind: 'markPaid' | 'void'; invoice: InvoiceListItem };

export function InvoicesTab({ client }: { client: PlatformApiClient }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useInvoices(client);
  const [query, setQuery] = useState('');
  const [status, setStatus] = useState('all');
  const [action, setAction] = useState<Action | null>(null);
  const [pending, setPending] = useState(false);

  async function confirm(reason: string) {
    if (action === null) return;
    setPending(true);
    try {
      if (action.kind === 'markPaid') {
        await client.markInvoicePaid(action.invoice.invoiceId, reason.length > 0 ? reason : null);
        toast({ title: t('platform.billing.markPaid.done'), variant: 'success' });
      } else {
        await client.voidInvoice(action.invoice.invoiceId, reason);
        toast({ title: t('platform.billing.void.done'), variant: 'success' });
      }
      setAction(null);
      if (state.status === 'ready') state.retry();
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') return <LoadingCards count={2} />;
  if (state.status === 'error') return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />;

  const rows = filterInvoices(state.data, { query, status });
  const actionable = (s: string) => s === 'issued' || s === 'overdue';

  return (
    <Card>
      <CardContent className="flex flex-col gap-3 pt-6">
        <div className="flex flex-wrap gap-2">
          <Input className="max-w-xs" placeholder={t('platform.billing.search.placeholder')} value={query} onChange={e => setQuery(e.target.value)} />
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger className="max-w-[200px]" aria-label={t('platform.billing.column.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {INVOICE_STATUS_FILTERS.map(s => (
                <SelectItem key={s} value={s}>{s === 'all' ? t('platform.billing.filter.allStatuses') : t(INVOICE_STATUS_LABEL[s])}</SelectItem>
              ))}
            </SelectContent>
          </Select>
        </div>
        {rows.length === 0 ? (
          <EmptyState message={t('platform.billing.empty.invoices')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.billing.column.number')}</TableHead>
                <TableHead>{t('platform.billing.column.tenant')}</TableHead>
                <TableHead>{t('platform.billing.column.kind')}</TableHead>
                <TableHead>{t('platform.billing.column.amount')}</TableHead>
                <TableHead>{t('platform.billing.column.status')}</TableHead>
                <TableHead>{t('platform.billing.column.due')}</TableHead>
                <TableHead>{t('platform.billing.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {rows.map(r => (
                <TableRow key={r.invoiceId}>
                  <TableCell className="tabular-nums">#{r.number}</TableCell>
                  <TableCell><span className="font-medium">{r.organizationName}</span> <code className="text-xs text-muted-foreground">{r.organizationSlug}</code></TableCell>
                  <TableCell>{INVOICE_KIND_LABEL[r.kind] ? t(INVOICE_KIND_LABEL[r.kind]) : r.kind}</TableCell>
                  <TableCell className="tabular-nums">{formatCurrency(r.amountMinorUnits, r.currencyCode)}</TableCell>
                  <TableCell><Badge variant={INVOICE_STATUS_VARIANT[r.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[r.status] ? t(INVOICE_STATUS_LABEL[r.status]) : r.status}</Badge></TableCell>
                  <TableCell>{formatDate(r.dueAtUtc)}</TableCell>
                  <TableCell className="flex gap-2">
                    {actionable(r.status) && (
                      <>
                        <Button variant="outline" onClick={() => setAction({ kind: 'markPaid', invoice: r })}>{t('platform.billing.action.markPaid')}</Button>
                        <Button variant="destructive" onClick={() => setAction({ kind: 'void', invoice: r })}>{t('platform.billing.action.void')}</Button>
                      </>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}
      </CardContent>
      <ConfirmDialog
        open={action !== null}
        title={action?.kind === 'void' ? t('platform.billing.void.title') : t('platform.billing.markPaid.title')}
        confirmLabel={action?.kind === 'void' ? t('platform.billing.void.confirm') : t('platform.billing.markPaid.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={action?.kind === 'void' ? t('platform.billing.void.reason') : t('platform.billing.markPaid.reference')}
        destructive={action?.kind === 'void'}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />
    </Card>
  );
}
```

> NOTE: `ConfirmDialog` always renders the reason input when `reasonLabel` is set. For void, reason is required by the backend; the dialog does not enforce non-empty, so the backend returns 400 on blank reason and the catch shows the error toast — acceptable. (Optional polish: disable confirm when void reason is empty — out of scope.)

- [ ] **Step 3: Run + commit**

Run: `npm test -- InvoicesTab` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/InvoicesTab.tsx src/AFK4.Platform.Web/src/platform/billing/InvoicesTab.test.tsx
git commit -m "feat(platform-web): billing invoices tab (mark-paid/void)"
```

---

### Task 14: `BillingScreen` (tabs container)

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/billing/BillingScreen.tsx` (+ `.test.tsx`)

- [ ] **Step 1: Implement `BillingScreen.tsx`**

```tsx
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/tabs';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import { SubscriptionsTab } from './SubscriptionsTab';
import { InvoicesTab } from './InvoicesTab';
import { PlansTab } from './PlansTab';

export function BillingScreen({ client }: { client: PlatformApiClient }) {
  const { t } = useI18n();
  return (
    <Tabs defaultValue="subscriptions" className="flex flex-col gap-4">
      <TabsList>
        <TabsTrigger value="subscriptions">{t('platform.billing.tab.subscriptions')}</TabsTrigger>
        <TabsTrigger value="invoices">{t('platform.billing.tab.invoices')}</TabsTrigger>
        <TabsTrigger value="plans">{t('platform.billing.tab.plans')}</TabsTrigger>
      </TabsList>
      <TabsContent value="subscriptions"><SubscriptionsTab client={client} /></TabsContent>
      <TabsContent value="invoices"><InvoicesTab client={client} /></TabsContent>
      <TabsContent value="plans"><PlansTab client={client} /></TabsContent>
    </Tabs>
  );
}
```

> NOTE: Confirm `Tabs`/`TabsList`/`TabsTrigger`/`TabsContent` exports + prop names against `src/components/ui/tabs.tsx` and an existing consumer (the club console uses tabs). Match the real API (`value`/`defaultValue` etc.).

- [ ] **Step 2: Write `BillingScreen.test.tsx`**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { BillingScreen } from './BillingScreen';

function fakeClient() {
  return {
    listSubscriptions: vi.fn().mockResolvedValue([]),
    listInvoices: vi.fn().mockResolvedValue([]),
    listPlans: vi.fn().mockResolvedValue([])
  } as never;
}

describe('BillingScreen', () => {
  it('renders the three tab triggers', async () => {
    render(<I18nProvider><ToastProvider><BillingScreen client={fakeClient()} /></ToastProvider></I18nProvider>);
    expect(screen.getByText('Подписки')).toBeInTheDocument();
    expect(screen.getByText('Инвойсы')).toBeInTheDocument();
    expect(screen.getByText('Тарифы')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('Подписок пока нет.')).toBeInTheDocument());
  });
});
```

- [ ] **Step 3: Run + commit**

Run: `npm test -- BillingScreen` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/billing/BillingScreen.tsx src/AFK4.Platform.Web/src/platform/billing/BillingScreen.test.tsx
git commit -m "feat(platform-web): billing screen tabs container"
```

---

## Phase F — Tenant-detail sections

### Task 15: `TenantSubscriptionSection` (replaces `TenantPlanSection`)

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/tenants/TenantSubscriptionSection.tsx` (+ `.test.tsx`)

This section loads the tenant's subscription (`getSubscription`), lets the admin change plan / interval / status / cancel-at-period-end via `updateSubscription` (`PATCH /subscription`), and shows period + next-invoice + amount. It supersedes the legacy `TenantPlanSection` (which used `PATCH /plan`).

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantSubscriptionSection } from './TenantSubscriptionSection';
import type { TenantSubscription } from '@/api/types';

const sub: TenantSubscription = {
  tenantSubscriptionId: 's', organizationId: 'o', planCode: 'starter', status: 'active',
  currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
  nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
  billingInterval: 'monthly', cancelAtPeriodEnd: false, createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
};

function fakeClient(over: Record<string, unknown> = {}) {
  return {
    getSubscription: vi.fn().mockResolvedValue(sub),
    updateSubscription: vi.fn().mockResolvedValue({ ...sub, planCode: 'growth' }),
    listPlans: vi.fn().mockResolvedValue([
      { planCode: 'starter', name: 'Starter', priceMinorUnits: 290000, currencyCode: 'RUB', billingInterval: 'monthly', maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, isActive: true, sortOrder: 0 },
      { planCode: 'growth', name: 'Growth', priceMinorUnits: 790000, currencyCode: 'RUB', billingInterval: 'monthly', maxBranches: null, maxDevicesPerBranch: null, maxConcurrentSessions: null, maxStaffUsersPerBranch: null, isActive: true, sortOrder: 1 }
    ]),
    ...over
  } as never;
}

describe('TenantSubscriptionSection', () => {
  it('loads and shows the current plan', async () => {
    render(<I18nProvider><ToastProvider><TenantSubscriptionSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Подписка')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Implement `TenantSubscriptionSection.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Switch } from '@/components/ui/switch';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from '@/components/ui/select';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { SubscriptionPlan, TenantSubscription } from '@/api/types';
import { SUBSCRIPTION_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<PlatformApiClient, 'getSubscription' | 'updateSubscription' | 'listPlans'>;

const STATUS_OPTIONS = ['trial', 'active', 'past_due', 'cancelled'] as const;
const INTERVAL_OPTIONS = ['monthly', 'yearly'] as const;

export function TenantSubscriptionSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [sub, setSub] = useState<TenantSubscription | null>(null);
  const [plans, setPlans] = useState<SubscriptionPlan[]>([]);
  const [error, setError] = useState(false);
  const [pending, setPending] = useState(false);
  const [planCode, setPlanCode] = useState('');
  const [interval, setInterval] = useState('');
  const [status, setStatus] = useState('');
  const [cancelAtPeriodEnd, setCancelAtPeriodEnd] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setSub(null); setError(false);
    Promise.all([client.getSubscription(organizationId), client.listPlans(true)])
      .then(([s, p]) => {
        if (cancelled) return;
        setSub(s); setPlans(p);
        setPlanCode(s.planCode); setInterval(s.billingInterval); setStatus(s.status); setCancelAtPeriodEnd(s.cancelAtPeriodEnd);
      })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function submit() {
    if (sub === null) return;
    setPending(true);
    try {
      const next = await client.updateSubscription(organizationId, {
        planCode: planCode !== sub.planCode ? planCode : null,
        billingInterval: interval !== sub.billingInterval ? interval : null,
        status: status !== sub.status ? status : null,
        cancelAtPeriodEnd: cancelAtPeriodEnd !== sub.cancelAtPeriodEnd ? cancelAtPeriodEnd : null
      });
      setSub(next);
      toast({ title: t('platform.tenant.subscriptionForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.tenant.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (error) return <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />;
  if (sub === null) return <LoadingCards count={1} />;

  const dirty = planCode !== sub.planCode || interval !== sub.billingInterval || status !== sub.status || cancelAtPeriodEnd !== sub.cancelAtPeriodEnd;

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.tenant.section.subscription')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-3 text-sm">
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.plan')}</span>
          <Select value={planCode} onValueChange={setPlanCode}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.plan')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {plans.map(p => <SelectItem key={p.planCode} value={p.planCode}>{p.name} ({formatCurrency(p.priceMinorUnits, p.currencyCode)})</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.interval')}</span>
          <Select value={interval} onValueChange={setInterval}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.interval')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {INTERVAL_OPTIONS.map(i => <SelectItem key={i} value={i}>{t(i === 'monthly' ? 'platform.billing.interval.monthly' : 'platform.billing.interval.yearly')}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="block">
          <span className="mb-1 block text-muted-foreground">{t('platform.tenant.subscriptionForm.status')}</span>
          <Select value={status} onValueChange={setStatus}>
            <SelectTrigger aria-label={t('platform.tenant.subscriptionForm.status')}><SelectValue /></SelectTrigger>
            <SelectContent>
              {STATUS_OPTIONS.map(s => <SelectItem key={s} value={s}>{t(SUBSCRIPTION_STATUS_LABEL[s])}</SelectItem>)}
            </SelectContent>
          </Select>
        </label>
        <label className="flex items-center justify-between">
          <span className="text-muted-foreground">{t('platform.tenant.subscriptionForm.cancelAtPeriodEnd')}</span>
          <Switch checked={cancelAtPeriodEnd} onCheckedChange={setCancelAtPeriodEnd} />
        </label>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.amount')}</span><span className="tabular-nums">{formatCurrency(sub.amountMinorUnits, sub.currencyCode)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.currentPeriod')}</span><span>{formatDate(sub.currentPeriodStartUtc)} – {formatDate(sub.currentPeriodEndUtc)}</span></div>
        <div className="flex justify-between text-muted-foreground"><span>{t('platform.tenant.subscriptionForm.nextInvoice')}</span><span>{sub.nextInvoiceUtc !== null ? formatDate(sub.nextInvoiceUtc) : '—'}</span></div>
        <div><Button onClick={() => void submit()} disabled={pending || !dirty}>{t('platform.tenant.subscriptionForm.apply')}</Button></div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 3: Run + commit**

Run: `npm test -- TenantSubscriptionSection` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/tenants/TenantSubscriptionSection.tsx src/AFK4.Platform.Web/src/platform/tenants/TenantSubscriptionSection.test.tsx
git commit -m "feat(platform-web): tenant subscription section (PATCH /subscription)"
```

---

### Task 16: `TenantInvoicesSection`

**Files:**
- Create: `src/AFK4.Platform.Web/src/platform/tenants/TenantInvoicesSection.tsx` (+ `.test.tsx`)

Loads `listTenantInvoices(orgId)`, shows them, and offers a "Generate invoice" button (`generateInvoice`).

- [ ] **Step 1: Implement `TenantInvoicesSection.tsx`**

```tsx
import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformApiClient } from '@/api/platformApi';
import type { Invoice } from '@/api/types';
import { INVOICE_STATUS_VARIANT, INVOICE_STATUS_LABEL } from '@/platform/billing/billingModel';

type Client = Pick<PlatformApiClient, 'listTenantInvoices' | 'generateInvoice'>;

export function TenantInvoicesSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [invoices, setInvoices] = useState<Invoice[] | null>(null);
  const [error, setError] = useState(false);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setInvoices(null); setError(false);
    client.listTenantInvoices(organizationId)
      .then(rows => { if (!cancelled) setInvoices(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function generate() {
    setPending(true);
    try {
      await client.generateInvoice(organizationId);
      toast({ title: t('platform.billing.generate.done'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.billing.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between">
        <CardTitle>{t('platform.tenant.section.invoices')}</CardTitle>
        <Button variant="outline" disabled={pending} onClick={() => void generate()}>{t('platform.tenant.invoices.generate')}</Button>
      </CardHeader>
      <CardContent className="flex flex-col gap-2 text-sm">
        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : invoices === null ? (
          <LoadingCards count={1} />
        ) : invoices.length === 0 ? (
          <EmptyState message={t('platform.tenant.invoices.empty')} />
        ) : (
          invoices.map(inv => (
            <div key={inv.invoiceId} className="flex items-center justify-between border-b border-border py-2 last:border-0">
              <span className="tabular-nums">#{inv.number} · {formatDate(inv.issuedAtUtc)}</span>
              <span className="flex items-center gap-2">
                <span className="tabular-nums">{formatCurrency(inv.amountMinorUnits, inv.currencyCode)}</span>
                <Badge variant={INVOICE_STATUS_VARIANT[inv.status] ?? 'outline'}>{INVOICE_STATUS_LABEL[inv.status] ? t(INVOICE_STATUS_LABEL[inv.status]) : inv.status}</Badge>
              </span>
            </div>
          ))
        )}
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 2: Write `TenantInvoicesSection.test.tsx`**

```tsx
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { TenantInvoicesSection } from './TenantInvoicesSection';

function fakeClient() {
  return {
    listTenantInvoices: vi.fn().mockResolvedValue([]),
    generateInvoice: vi.fn().mockResolvedValue({})
  } as never;
}

describe('TenantInvoicesSection', () => {
  it('shows empty state after load', async () => {
    render(<I18nProvider><ToastProvider><TenantInvoicesSection client={fakeClient()} organizationId="o" /></ToastProvider></I18nProvider>);
    await waitFor(() => expect(screen.getByText('Инвойсов пока нет.')).toBeInTheDocument());
  });
});
```

- [ ] **Step 3: Run + commit**

Run: `npm test -- TenantInvoicesSection` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/tenants/TenantInvoicesSection.tsx src/AFK4.Platform.Web/src/platform/tenants/TenantInvoicesSection.test.tsx
git commit -m "feat(platform-web): tenant invoices section"
```

---

### Task 17: Wire sections into `TenantDrawer`; delete `TenantPlanSection`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/platform/tenants/TenantDrawer.tsx`
- Delete: `src/AFK4.Platform.Web/src/platform/tenants/TenantPlanSection.tsx`
- Delete: `src/AFK4.Platform.Web/src/platform/tenants/TenantPlanSection.test.tsx`

- [ ] **Step 1: Update `TenantDrawer.tsx` imports**

Replace the `TenantPlanSection` import line:

```ts
import { TenantPlanSection } from './TenantPlanSection';
```

with:

```ts
import { TenantSubscriptionSection } from './TenantSubscriptionSection';
import { TenantInvoicesSection } from './TenantInvoicesSection';
```

- [ ] **Step 2: Update the section composition**

Replace the line:

```tsx
      <TenantPlanSection client={client} tenant={tenant} onUpdated={handleUpdated} />
```

with:

```tsx
      <TenantSubscriptionSection client={client} organizationId={tenant.organizationId} />
```

Then, immediately after the `<TenantLimitsSection ... />` line, add:

```tsx
      <TenantInvoicesSection client={client} organizationId={tenant.organizationId} />
```

> NOTE: `TenantSubscriptionSection` is the source of truth for plan/status now, so the drawer no longer needs `onUpdated` for the plan; status changes still flow through `TenantStatusSection`. The subscription section manages its own load/save (it does not mutate the parent `TenantDetail`). This is intentional — billing state lives in the subscription entity, not the `TenantDetail` DTO.

- [ ] **Step 3: Delete the legacy plan section + its test**

```bash
git rm src/AFK4.Platform.Web/src/platform/tenants/TenantPlanSection.tsx src/AFK4.Platform.Web/src/platform/tenants/TenantPlanSection.test.tsx
```

- [ ] **Step 4: Check for stragglers**

Search the repo for any remaining `TenantPlanSection` references (there should be none after the drawer edit):

Run: `Grep TenantPlanSection` across `src/AFK4.Platform.Web/src`.
Expected: no matches.

- [ ] **Step 5: Run + commit**

Run: `npm test` (full suite) then `npm run build`.
Expected: both green.

```bash
git add src/AFK4.Platform.Web/src/platform/tenants/TenantDrawer.tsx
git commit -m "feat(platform-web): drawer uses subscription+invoices sections; drop legacy TenantPlanSection"
```

---

## Phase G — Overview KPIs, nav, routing

### Task 18: Billing KPI tiles on Overview

**Files:**
- Modify: `src/AFK4.Platform.Web/src/platform/overview/OverviewScreen.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx` (`PlatformArea` passes a metrics state to Overview)

Add an optional `billing` prop (a `BillingMetricsState`) to `OverviewScreen`. When ready, render three extra KPI tiles (MRR / outstanding / overdue). Overview's existing tenant KPIs are unchanged.

- [ ] **Step 1: Update `OverviewScreen.tsx`**

Add imports at the top:

```ts
import type { BillingMetricsState } from '@/platform/billing/useBillingMetrics';
```

Change the component signature and the ready-branch render. Replace:

```tsx
export function OverviewScreen({ state }: { state: TenantMetricsState }) {
  const { t, formatNumber } = useI18n();
```

with:

```tsx
export function OverviewScreen({ state, billing }: { state: TenantMetricsState; billing?: BillingMetricsState }) {
  const { t, formatNumber, formatCurrency } = useI18n();
```

Then, inside the `ready` return (after the closing `</div>` of the first KPI grid at line ~51, before the by-plan grid), insert a billing KPI row:

```tsx
      {billing !== undefined && billing.status === 'ready' && (
        <div className="grid grid-cols-1 gap-4 md:grid-cols-3">
          <Kpi label={t('platform.overview.kpi.mrr')} value={formatCurrency(billing.data.mrrMinorUnits, billing.data.currencyCode)} />
          <Kpi label={t('platform.overview.kpi.outstanding')} value={formatCurrency(billing.data.outstandingMinorUnits, billing.data.currencyCode)} />
          <Kpi label={t('platform.overview.kpi.overdue')} value={formatCurrency(billing.data.overdueMinorUnits, billing.data.currencyCode)} />
        </div>
      )}
```

- [ ] **Step 2: Update `PlatformArea` in `App.tsx`**

Find `const metricsState = useTenantMetrics(adminClient);` (line ~503) and add below it:

```tsx
  const billingMetricsState = useBillingMetrics(adminClient);
```

Add the import near the other platform imports at the top of `App.tsx`:

```ts
import { useBillingMetrics } from '@/platform/billing/useBillingMetrics';
```

Then pass it to the overview render. Replace:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} />
```

with:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
```

- [ ] **Step 3: Update the Overview test**

In `src/platform/overview/OverviewScreen.test.tsx`, the existing tests pass only `state`; `billing` is optional so they still compile. Add one test asserting the MRR tile appears when `billing` is ready:

```tsx
it('renders billing KPI tiles when billing metrics are ready', () => {
  const ready = { status: 'ready' as const, data: { totalTenants: 0, activeTenants: 0, suspendedTenants: 0, trialTenants: 0, totalBranches: 0, newTenants30d: 0 }, byPlan: [], attention: [] };
  // Reuse however the existing test builds a ready TenantMetricsState; if a helper exists, use it.
});
```

> NOTE: The existing `OverviewScreen.test.tsx` already constructs a ready `TenantMetricsState`. Mirror that construction, then pass `billing={{ status: 'ready', data: { mrrMinorUnits: 580000, currencyCode: 'RUB', activeSubscriptions: 2, outstandingMinorUnits: 0, outstandingCount: 0, overdueMinorUnits: 0, overdueCount: 0 }, retry: () => {} }}` and assert `screen.getByText('MRR')` is present. Fill in the body using the existing test's patterns rather than inventing a new harness.

- [ ] **Step 4: Run + commit**

Run: `npm test -- OverviewScreen` then `npm run build`.

```bash
git add src/AFK4.Platform.Web/src/platform/overview/OverviewScreen.tsx src/AFK4.Platform.Web/src/platform/overview/OverviewScreen.test.tsx src/AFK4.Platform.Web/src/App.tsx
git commit -m "feat(platform-web): MRR/outstanding/overdue KPI tiles on overview"
```

---

### Task 19: `/admin/billing` route + nav flip

**Files:**
- Modify: `src/AFK4.Platform.Web/src/platform/nav.ts`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

The platform route union is `AdminRoute` (kinds: `adminOverview`, `tenantList`, `newTenant`, `tenantDetail`). Add `adminBilling` and wire it through `resolvePlatformRoute`, `pathForAdminRoute`, `PLATFORM_SCREEN_TITLE`, `PlatformArea`'s render, and `handleNavigate`.

- [ ] **Step 1: Flip the nav `soon` flag**

In `nav.ts`, change the `billing` item:

```ts
      { key: 'billing', labelKey: 'nav.platform.billing', path: '/admin/billing', ownerOnly: false, soon: true }
```

to:

```ts
      { key: 'billing', labelKey: 'nav.platform.billing', path: '/admin/billing', ownerOnly: false, soon: false }
```

- [ ] **Step 2: Add `adminBilling` to the `AdminRoute` union**

In `App.tsx`, find the `AdminRoute` type (the union containing `| { kind: 'adminOverview' }` at line ~39) and add:

```ts
  | { kind: 'adminBilling' }
```

- [ ] **Step 3: Add to `PLATFORM_SCREEN_TITLE` and `pathForAdminRoute`**

In `PLATFORM_SCREEN_TITLE` (line ~479) add:

```ts
  adminBilling: 'Биллинг',
```

In `pathForAdminRoute` (line ~486) add a case:

```ts
    case 'adminBilling':
      return '/admin/billing';
```

- [ ] **Step 4: Resolve `/admin/billing` in `resolvePlatformRoute`**

In `resolvePlatformRoute` (line ~557), inside the `allowsAdminRoutes(audience)` block, add a branch that maps the path `/billing` (after `normalizePath` strips the `/admin` prefix — confirm how existing admin paths like `/tenants` are matched, line ~571, and mirror it):

```ts
    if (path === '/billing') {
      return { route: { kind: 'adminBilling' } };
    }
```

> NOTE: Match the exact path form the existing cases use. The existing `/tenants` case (line ~571) shows admin paths are matched WITHOUT the `/admin` prefix here (the audience layer strips it) and the canonical path uses `/admin/...` for `redirectTo`. If `/tenants` returns `{ route, redirectTo: '/admin/tenants' }`, then use `{ route: { kind: 'adminBilling' }, redirectTo: '/admin/billing' }` to match. Follow whatever the sibling tenants case does verbatim.

- [ ] **Step 5: Render `BillingScreen` in `PlatformArea`**

Add the import near the other platform screen imports in `App.tsx`:

```ts
import { BillingScreen as PlatformBillingScreen } from '@/platform/billing/BillingScreen';
```

In `PlatformArea`'s render, change the route branch chain. Replace:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'newTenant' ? (
```

with:

```tsx
      {route.kind === 'adminOverview' ? (
        <PlatformOverviewScreen state={metricsState} billing={billingMetricsState} />
      ) : route.kind === 'adminBilling' ? (
        <PlatformBillingScreen client={adminClient} />
      ) : route.kind === 'newTenant' ? (
```

- [ ] **Step 6: Verify `handleNavigate` no longer drops billing**

`handleNavigate` in `PlatformArea` (line ~505) calls `resolvePlatformRoute` then `isAdminRoute`. Now that `adminBilling` is a real route, confirm `isAdminRoute` (the type guard) returns true for it — find `isAdminRoute` (referenced line ~507; defined elsewhere in `App.tsx`) and ensure its check admits `adminBilling`. If it enumerates kinds explicitly, add `'adminBilling'`; if it checks against a set of admin kinds, add it there.

- [ ] **Step 7: Run + commit**

Run: `npm test` (full suite) then `npm run build`.
Expected: both green; navigating to `/admin/billing` resolves to the billing screen.

```bash
git add src/AFK4.Platform.Web/src/platform/nav.ts src/AFK4.Platform.Web/src/App.tsx
git commit -m "feat(platform-web): wire /admin/billing route + flip nav soon flag"
```

---

## Phase H — Verification

### Task 20: Full-stack gate + manual smoke

**Files:** none (verification only)

- [ ] **Step 1: Backend gate**

Run (repo root): `dotnet build` then `dotnet test`
Expected: Build 0 errors; all tests pass (baseline 586 + the ~9 new billing tests).

- [ ] **Step 2: Frontend gate**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: `tsc -b` + `vite build` succeed; Vitest all green (the new billing/section tests + the existing parity test).

- [ ] **Step 3: Confirm no `TenantPlanSection` / legacy `/plan`-from-UI references remain**

Run: `Grep "updatePlan\b"` and `Grep "TenantPlanSection"` across `src/AFK4.Platform.Web/src`.
Expected: `TenantPlanSection` → 0 matches. `updatePlan` may still exist as a client method (legacy endpoint retained per Plan 3) but should have NO remaining UI callers — note any caller found and remove it (the only intended caller was the deleted `TenantPlanSection`).

- [ ] **Step 4: Manual verification (preview)**

Use the preview tooling (`preview_start` against `src/AFK4.Platform.Web` with `VITE_AUDIENCE=admin`, backend running) to confirm:
1. `/admin/billing` shows the three tabs; Подписки and Инвойсы load; Тарифы shows starter/growth/scale; "New plan" opens the dialog.
2. Opening a tenant drawer shows the Subscription section (plan/interval/status/cancel) and the Invoices section with a working "Generate invoice".
3. Overview shows MRR / Outstanding / Overdue tiles.
4. Mark-paid and Void on an invoice show success toasts and the row updates after reload.

Report results with a screenshot. Do not claim completion until the gates in Steps 1–2 pass with output shown.

- [ ] **Step 5: Final commit (if any verification fixups were needed)**

```bash
git add -A
git commit -m "test(platform): Plan 4 billing admin UI verification fixups"
```

---

## Self-Review (completed during authoring)

**Spec coverage:** Биллинг area Подписки/Инвойсы/Тарифы → Tasks 11–14; tenant-detail subscription + invoices sections → Tasks 15–17; MRR/outstanding/overdue KPIs → Tasks 5 + 18; cross-tenant data (the gap the owner approved going full-stack on) → Tasks 1–5; nav flip + route → Task 19; migrate tenant plan UI off `PATCH /plan` onto `PATCH /subscription` (Plan-3 deferred follow-up) → Tasks 15 + 17. Club-side billing (`/club/billing`) remains Plan 7 — out of scope here.

**Type consistency:** Frontend `SubscriptionPlan`/`Invoice`/`TenantSubscription`/`SubscriptionListItem`/`InvoiceListItem`/`PlatformBillingMetrics` mirror the C# DTO field names (camelCased). New client methods (`listPlans`, `createPlan`, `updatePlanCatalog`, `getSubscription`, `updateSubscription`, `listTenantInvoices`, `listSubscriptions`, `listInvoices`, `getBillingMetrics`, `generateInvoice`, `markInvoicePaid`, `voidInvoice`) are referenced consistently in hooks/tabs/sections. `updatePlanCatalog` is deliberately named to avoid colliding with the existing legacy `updatePlan(orgId, planCode, subscriptionStatus)`.

**Open items flagged inline (verify against real source during implementation, do not assume):** `BadgeVariant` values; `Switch` callback prop; `Tabs`/`Table`/`EmptyState`/`ToastProvider` import paths & APIs; `formatCurrency` signature; `OrganizationEntity` required columns in test seeds; the exact `resolvePlatformRoute`/`isAdminRoute` path-matching form; the existing DI registration site for billing services. These are codebase facts the engineer can confirm in seconds; every one has a NOTE telling them where to look and what to match.
