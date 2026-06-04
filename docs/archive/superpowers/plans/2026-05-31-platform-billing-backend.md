# Platform Billing Backend (SaaS Subscriptions & Invoicing) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a SaaS subscription/invoicing backend to `AFK4.Platform.Api` — a plan catalog, per-tenant subscriptions (the new source of truth for plan/status), invoices with proration and an hourly generation job, and platform-admin endpoints — so the `/admin` control plane can manage tenant billing.

**Architecture:** Three new EF entities (`SubscriptionPlanEntity`, `TenantSubscriptionEntity`, `InvoiceEntity`) in `PlatformDbContext`, behind a Postgres migration. Plans are seeded by an `IHostedService` (not migration data) so the in-memory test DB also gets them. A `TenantSubscriptionEntity` becomes the source of truth for a tenant's plan/status; the denormalized `OrganizationEntity.PlanCode/SubscriptionStatus/LimitsJson` are kept in sync on every subscription write so `TenantSuspensionMiddleware`/`ITenantStatusGuard` keep working unchanged. Subscriptions are created lazily (`EnsureSubscriptionAsync`) and on tenant creation. A pure `IInvoiceGenerationRunner` holds the generation/period-advance/overdue logic; an `InvoiceGenerationHostedService : BackgroundService` calls it hourly. All endpoints follow the existing platform pattern: `RequirePermission` → (financial actions: idempotency read) → service → audit → (idempotency write).

**Tech Stack:** ASP.NET minimal APIs (.NET 10), EF Core + Npgsql, xUnit + EF InMemory. Money = `long` minor units + `string` currency code (default `RUB`). DTOs in `AFK4.Shared.Contracts`, services under `AFK4.Platform.Api.Platform.Billing`.

---

## Design decisions (read before starting)

These resolve ambiguities in the design spec (`docs/superpowers/specs/2026-05-31-platform-admin-control-plane-design.md`, §3 & §7). They are locked for this plan:

1. **Subscription = source of truth.** `ITenantSubscriptionService` writes the subscription row **and** mirrors `PlanCode`, `SubscriptionStatus`, and the plan's limits into `OrganizationEntity`. The legacy `PATCH /api/platform/tenants/{id}/plan` endpoint and `UpdatePlanAsync` are **left intact and functional** (they still update the org's denormalized fields) so existing tests/clients don't break; they become legacy and are removed in a later plan once the UI migrates. The new subscription endpoint is the primary surface.
2. **Lazy + eager subscription creation.** `EnsureSubscriptionAsync(org)` creates a subscription on first `GET/PATCH .../subscription` for legacy tenants, deriving amount/interval/limits from the catalog plan that matches `org.PlanCode` (fallback: amount `0`, interval `monthly`, currency `RUB`). New tenants also get a subscription seeded in `EfPlatformTenantService.CreateAsync`.
3. **Plan-catalog natural key.** `SubscriptionPlanEntity.PlanCode` is the primary key (string). Plan limits are `int?` (null = unlimited) so they copy straight into `TenantLimitsDto`.
4. **Invoice numbering = global sequential `int`, gaps allowed.** A new invoice's `Number = max(existing Number) + 1`. Generation saves per-subscription, so the `MAX` reflects committed rows (no collisions). Voided/skipped numbers create gaps — acceptable.
5. **Recurring-job idempotency = DB existence check (not the HTTP idempotency store).** The background job has no admin user, so reusing `IPlatformIdempotencyStore` (which requires a `platformAdminUserId` + HTTP response body) is a poor fit. Instead `GenerateForSubscriptionAsync` skips if a non-void **subscription-kind** invoice already exists for `(OrganizationId, CurrentPeriodStartUtc)`. The HTTP idempotency store is still used on the **admin-triggered** financial POST endpoints (generate/mark-paid/void).
6. **Invoice `Kind`** column (`"subscription"` | `"proration"`) distinguishes recurring invoices from mid-cycle proration adjustments so both can coexist in one period.
7. **Proration = upgrades only.** Mid-cycle plan change computes `proration = round((newDaily − oldDaily) × remainingDays)`. If `proration > 0` (upgrade) a one-off `Kind=proration` invoice is issued for `[now, CurrentPeriodEndUtc]`. If `≤ 0` (downgrade/no change) **no** invoice is issued — the new (lower) amount applies from the next cycle. (Out of scope per spec: refunds/credits.) The subscription's `AmountMinorUnits`/`PlanCode`/`BillingInterval` update immediately.
8. **Due date / overdue.** `DueAtUtc = IssuedAtUtc + BillingOptions.InvoiceDueAfter` (default 7 days). Each run flips `issued → overdue` for invoices past `DueAtUtc`.
9. **No backend metrics changes.** Plan 1's "metrics" are derived client-side from `listTenants()`; there is no `/api/platform/metrics` endpoint. MRR/outstanding KPIs are Plan 4 (UI) work, out of scope here.

### Money / format conventions (used in every task)
- `long AmountMinorUnits` + `string CurrencyCode` (3 chars), default `"RUB"`.
- `BillingIntervalNames`: `"monthly"`, `"yearly"`. Period advance: monthly → `AddMonths(1)`, yearly → `AddYears(1)`.
- `InvoiceStatusNames`: `"issued"`, `"paid"`, `"void"`, `"overdue"`.
- `InvoiceKindNames`: `"subscription"`, `"proration"`.

### File structure (created/modified)
- **Contracts** (`src/AFK4.Shared.Contracts/Platform/Billing/`): `BillingIntervalNames.cs`, `InvoiceStatusNames.cs`, `InvoiceKindNames.cs`, `SubscriptionPlanDto.cs`, `CreatePlanRequest.cs`, `UpdatePlanRequest.cs`, `TenantSubscriptionDto.cs`, `UpdateSubscriptionRequest.cs`, `InvoiceDto.cs`, `MarkInvoicePaidRequest.cs`, `VoidInvoiceRequest.cs`.
- **Contracts (modify)**: `Platform/Auth/PlatformAdminPermissionNames.cs`.
- **Entities** (`src/AFK4.Platform.Api/Data/`): `SubscriptionPlanEntity.cs`, `TenantSubscriptionEntity.cs`, `InvoiceEntity.cs`; modify `PlatformDbContext.cs`.
- **Services** (`src/AFK4.Platform.Api/Platform/Billing/`): `BillingOperationResult.cs`, `BillingOptions.cs`, `IPlanCatalogService.cs` + `EfPlanCatalogService.cs`, `BillingPlanSeedHostedService.cs`, `ITenantSubscriptionService.cs` + `EfTenantSubscriptionService.cs`, `IInvoiceGenerationRunner.cs` + `EfInvoiceGenerationRunner.cs`, `IInvoiceService.cs` + `EfInvoiceService.cs`, `InvoiceGenerationHostedService.cs`.
- **Modify**: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`, `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`, `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs`, `src/AFK4.Platform.Api/Program.cs`.
- **Tests** (`tests/AFK4.Platform.Api.Tests/Billing/` and `/Platform/`): `FixedTimeProvider.cs`, `EfPlanCatalogServiceTests.cs`, `BillingPlanSeedHostedServiceTests.cs`, `PlatformPlanEndpointTests.cs`, `EfTenantSubscriptionServiceTests.cs`, `PlatformSubscriptionEndpointTests.cs`, `EfInvoiceGenerationRunnerTests.cs`, `EfInvoiceServiceTests.cs`, `PlatformInvoiceEndpointTests.cs`.

### Build / test gates
```bash
dotnet build AFK4.sln
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj
```
Migration command (Postgres; in-memory tests don't run it):
```bash
dotnet ef migrations add AddSaasSubscriptionBilling -p src/AFK4.Platform.Api -s src/AFK4.Platform.Api
```

---

## Task 1: Billing name constants, permissions, and audit actions

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/BillingIntervalNames.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/InvoiceStatusNames.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/InvoiceKindNames.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Audit/AuditActionNames.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs`

- [ ] **Step 1: Create the three name-constant files**

`BillingIntervalNames.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public static class BillingIntervalNames
{
    public const string Monthly = "monthly";

    public const string Yearly = "yearly";
}
```

`InvoiceStatusNames.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public static class InvoiceStatusNames
{
    public const string Issued = "issued";

    public const string Paid = "paid";

    public const string Void = "void";

    public const string Overdue = "overdue";
}
```

`InvoiceKindNames.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public static class InvoiceKindNames
{
    public const string Subscription = "subscription";

    public const string Proration = "proration";
}
```

- [ ] **Step 2: Add billing permissions**

In `PlatformAdminPermissionNames.cs`, add these constants alongside the existing ones (after `ViewPlatformAudit`):
```csharp
    public const string ViewBilling = "platform.billing.view";

    public const string ManagePlans = "platform.billing.plans.manage";

    public const string ManageSubscriptions = "platform.billing.subscriptions.manage";

    public const string ManageInvoices = "platform.billing.invoices.manage";
```

- [ ] **Step 3: Add billing audit action names**

In `AuditActionNames.cs`, add alongside the tenancy actions:
```csharp
    public const string ViewBilling = "billing.view";

    public const string CreatePlan = "billing.plan.create";

    public const string UpdatePlan = "billing.plan.update";

    public const string UpdateSubscription = "billing.subscription.update";

    public const string GenerateInvoice = "billing.invoice.generate";

    public const string MarkInvoicePaid = "billing.invoice.mark_paid";

    public const string VoidInvoice = "billing.invoice.void";
```

- [ ] **Step 4: Grant permissions in the role catalog**

In `PlatformAdminPermissionCatalog.cs`, add to the `PlatformOwner` permission set (all four):
```csharp
                PlatformAdminPermissionNames.ViewBilling,
                PlatformAdminPermissionNames.ManagePlans,
                PlatformAdminPermissionNames.ManageSubscriptions,
                PlatformAdminPermissionNames.ManageInvoices,
```
And add to the `PlatformSupport` set (view only):
```csharp
                PlatformAdminPermissionNames.ViewBilling,
```

- [ ] **Step 5: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS (no consumers yet; this is additive).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Shared.Contracts/Platform/Billing src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs src/AFK4.Platform.Api/Audit/AuditActionNames.cs src/AFK4.Platform.Api/Platform/Identity/PlatformAdminPermissionCatalog.cs
git commit -m "feat(platform): billing name constants, permissions, audit actions"
```

---

## Task 2: Billing DTOs (contracts)

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/SubscriptionPlanDto.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/CreatePlanRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/UpdatePlanRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/TenantSubscriptionDto.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/UpdateSubscriptionRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/InvoiceDto.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/MarkInvoicePaidRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Billing/VoidInvoiceRequest.cs`

- [ ] **Step 1: Create all DTO files**

`SubscriptionPlanDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record SubscriptionPlanDto(
    string PlanCode,
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    bool IsActive,
    int SortOrder);
```

`CreatePlanRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record CreatePlanRequest(
    string PlanCode,
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    int SortOrder);
```

`UpdatePlanRequest.cs` (all mutable fields; `PlanCode` is the route key, not in the body):
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record UpdatePlanRequest(
    string Name,
    long PriceMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    int? MaxBranches,
    int? MaxDevicesPerBranch,
    int? MaxConcurrentSessions,
    int? MaxStaffUsersPerBranch,
    bool IsActive,
    int SortOrder);
```

`TenantSubscriptionDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record TenantSubscriptionDto(
    Guid TenantSubscriptionId,
    Guid OrganizationId,
    string PlanCode,
    string Status,
    DateTimeOffset CurrentPeriodStartUtc,
    DateTimeOffset CurrentPeriodEndUtc,
    DateTimeOffset? NextInvoiceUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string BillingInterval,
    bool CancelAtPeriodEnd,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
```

`UpdateSubscriptionRequest.cs` (all optional; only provided fields apply; `PlanCode` change triggers proration):
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record UpdateSubscriptionRequest(
    string? PlanCode,
    string? BillingInterval,
    string? Status,
    bool? CancelAtPeriodEnd);
```

`InvoiceDto.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record InvoiceDto(
    Guid InvoiceId,
    Guid OrganizationId,
    int Number,
    string Kind,
    DateTimeOffset PeriodStartUtc,
    DateTimeOffset PeriodEndUtc,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset DueAtUtc,
    long AmountMinorUnits,
    string CurrencyCode,
    string Status,
    DateTimeOffset? PaidAtUtc,
    DateTimeOffset? VoidedAtUtc,
    string? VoidReason,
    string Description);
```

`MarkInvoicePaidRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record MarkInvoicePaidRequest(string? Reference);
```

`VoidInvoiceRequest.cs`:
```csharp
namespace AFK4.Shared.Contracts.Platform.Billing;

public sealed record VoidInvoiceRequest(string Reason);
```

- [ ] **Step 2: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 3: Commit**

```bash
git add src/AFK4.Shared.Contracts/Platform/Billing
git commit -m "feat(platform): billing DTOs (plan, subscription, invoice)"
```

---

## Task 3: `SubscriptionPlanEntity` + DbSet + EF config

**Files:**
- Create: `src/AFK4.Platform.Api/Data/SubscriptionPlanEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class SubscriptionPlanEntity
{
    public string PlanCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public long PriceMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public string BillingInterval { get; set; } = "monthly";
    public int? MaxBranches { get; set; }
    public int? MaxDevicesPerBranch { get; set; }
    public int? MaxConcurrentSessions { get; set; }
    public int? MaxStaffUsersPerBranch { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Add the DbSet**

In `PlatformDbContext.cs`, add alongside the other `DbSet` properties (e.g. after `PlatformIdempotencyRecords`):
```csharp
    public DbSet<SubscriptionPlanEntity> SubscriptionPlans => Set<SubscriptionPlanEntity>();
```

- [ ] **Step 3: Configure the entity in `OnModelCreating`**

In `PlatformDbContext.cs`, inside `OnModelCreating`, add after the `OrganizationEntity` configuration block:
```csharp
        modelBuilder.Entity<SubscriptionPlanEntity>(entity =>
        {
            entity.ToTable("subscription_plans");
            entity.HasKey(plan => plan.PlanCode);
            entity.Property(plan => plan.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(plan => plan.Name).HasMaxLength(160).IsRequired();
            entity.Property(plan => plan.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(plan => plan.BillingInterval).HasMaxLength(16).IsRequired();
            entity.HasIndex(plan => plan.SortOrder);
        });
```

- [ ] **Step 4: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/SubscriptionPlanEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs
git commit -m "feat(platform): SubscriptionPlanEntity + EF mapping"
```

---

## Task 4: `TenantSubscriptionEntity` + DbSet + EF config

**Files:**
- Create: `src/AFK4.Platform.Api/Data/TenantSubscriptionEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class TenantSubscriptionEntity
{
    public Guid TenantSubscriptionId { get; set; }
    public Guid OrganizationId { get; set; }
    public string PlanCode { get; set; } = string.Empty;
    public string Status { get; set; } = "trial";
    public DateTimeOffset CurrentPeriodStartUtc { get; set; }
    public DateTimeOffset CurrentPeriodEndUtc { get; set; }
    public DateTimeOffset? NextInvoiceUtc { get; set; }
    public long AmountMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public string BillingInterval { get; set; } = "monthly";
    public bool CancelAtPeriodEnd { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Add the DbSet**

In `PlatformDbContext.cs`:
```csharp
    public DbSet<TenantSubscriptionEntity> TenantSubscriptions => Set<TenantSubscriptionEntity>();
```

- [ ] **Step 3: Configure in `OnModelCreating`** (after the `SubscriptionPlanEntity` block)

```csharp
        modelBuilder.Entity<TenantSubscriptionEntity>(entity =>
        {
            entity.ToTable("tenant_subscriptions");
            entity.HasKey(subscription => subscription.TenantSubscriptionId);
            entity.Property(subscription => subscription.PlanCode).HasMaxLength(64).IsRequired();
            entity.Property(subscription => subscription.Status).HasMaxLength(32).IsRequired();
            entity.Property(subscription => subscription.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(subscription => subscription.BillingInterval).HasMaxLength(16).IsRequired();
            entity.HasIndex(subscription => subscription.OrganizationId).IsUnique();
            entity.HasIndex(subscription => new { subscription.Status, subscription.NextInvoiceUtc });
        });
```

- [ ] **Step 4: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/TenantSubscriptionEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs
git commit -m "feat(platform): TenantSubscriptionEntity + EF mapping"
```

---

## Task 5: `InvoiceEntity` + DbSet + EF config

**Files:**
- Create: `src/AFK4.Platform.Api/Data/InvoiceEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace AFK4.Platform.Api.Data;

public sealed class InvoiceEntity
{
    public Guid InvoiceId { get; set; }
    public Guid OrganizationId { get; set; }
    public int Number { get; set; }
    public string Kind { get; set; } = "subscription";
    public DateTimeOffset PeriodStartUtc { get; set; }
    public DateTimeOffset PeriodEndUtc { get; set; }
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public long AmountMinorUnits { get; set; }
    public string CurrencyCode { get; set; } = "RUB";
    public string Status { get; set; } = "issued";
    public DateTimeOffset? PaidAtUtc { get; set; }
    public DateTimeOffset? VoidedAtUtc { get; set; }
    public string? VoidReason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Add the DbSet**

In `PlatformDbContext.cs`:
```csharp
    public DbSet<InvoiceEntity> Invoices => Set<InvoiceEntity>();
```

- [ ] **Step 3: Configure in `OnModelCreating`** (after the `TenantSubscriptionEntity` block)

```csharp
        modelBuilder.Entity<InvoiceEntity>(entity =>
        {
            entity.ToTable("invoices");
            entity.HasKey(invoice => invoice.InvoiceId);
            entity.Property(invoice => invoice.Kind).HasMaxLength(16).IsRequired();
            entity.Property(invoice => invoice.CurrencyCode).HasMaxLength(3).IsRequired();
            entity.Property(invoice => invoice.Status).HasMaxLength(16).IsRequired();
            entity.Property(invoice => invoice.VoidReason).HasMaxLength(512);
            entity.Property(invoice => invoice.Description).HasMaxLength(240).IsRequired();
            entity.HasIndex(invoice => invoice.Number).IsUnique();
            entity.HasIndex(invoice => new { invoice.OrganizationId, invoice.IssuedAtUtc });
            entity.HasIndex(invoice => new { invoice.Status, invoice.DueAtUtc });
        });
```

- [ ] **Step 4: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/InvoiceEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs
git commit -m "feat(platform): InvoiceEntity + EF mapping"
```

---

## Task 6: EF migration for the billing tables

**Files:**
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddSaasSubscriptionBilling.cs` (+ `.Designer.cs`) and updated `PlatformDbContextModelSnapshot.cs` (generated)

- [ ] **Step 1: Generate the migration**

Run:
```bash
dotnet ef migrations add AddSaasSubscriptionBilling -p src/AFK4.Platform.Api -s src/AFK4.Platform.Api
```
Expected: three new tables (`subscription_plans`, `tenant_subscriptions`, `invoices`) in the generated `Up()`, plus snapshot changes. If `dotnet ef` is not installed, run `dotnet tool install --global dotnet-ef` first.

- [ ] **Step 2: Build to confirm the generated migration compiles**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 3: Sanity-check the generated `Up()`**

Open the generated migration and confirm it creates exactly the three tables with the columns/indexes from Tasks 3–5 (string PK `PlanCode` on `subscription_plans`, unique `OrganizationId` on `tenant_subscriptions`, unique `Number` on `invoices`). No data seeding here (plans are seeded by the hosted service in Task 9).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Data/Migrations
git commit -m "feat(platform): migration for subscription/invoice billing tables"
```

---

## Task 7: `BillingOperationResult<T>` + `BillingOptions`

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/BillingOperationResult.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/BillingOptions.cs`

- [ ] **Step 1: Create the result type** (mirrors `PlatformTenantOperationResult<T>`)

```csharp
namespace AFK4.Platform.Api.Platform.Billing;

public enum BillingOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound
}

public sealed record BillingOperationResult<T>(
    BillingOperationStatus Status,
    T? Value,
    string? Error)
    where T : class
{
    public bool Succeeded => Status == BillingOperationStatus.Succeeded;

    public static BillingOperationResult<T> Success(T value) =>
        new(BillingOperationStatus.Succeeded, value, null);

    public static BillingOperationResult<T> BadRequest(string error) =>
        new(BillingOperationStatus.BadRequest, null, error);

    public static BillingOperationResult<T> Conflict(string error) =>
        new(BillingOperationStatus.Conflict, null, error);

    public static BillingOperationResult<T> NotFound(string error) =>
        new(BillingOperationStatus.NotFound, null, error);
}
```

- [ ] **Step 2: Create the options**

```csharp
namespace AFK4.Platform.Api.Platform.Billing;

public sealed class BillingOptions
{
    public const string ConfigurationSection = "Billing";

    /// <summary>How long after issue an invoice is due before it flips to overdue.</summary>
    public TimeSpan InvoiceDueAfter { get; set; } = TimeSpan.FromDays(7);

    /// <summary>How often the invoice-generation background job ticks.</summary>
    public TimeSpan GenerationInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Default currency for lazily-created subscriptions when no catalog plan matches.</summary>
    public string DefaultCurrencyCode { get; set; } = "RUB";
}
```

- [ ] **Step 3: Build**

Run: `dotnet build AFK4.sln`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/BillingOperationResult.cs src/AFK4.Platform.Api/Platform/Billing/BillingOptions.cs
git commit -m "feat(platform): billing operation result + options"
```

---

## Task 8: Plan catalog service (`IPlanCatalogService` / `EfPlanCatalogService`)

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/IPlanCatalogService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfPlanCatalogService.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Billing/FixedTimeProvider.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfPlanCatalogServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI registration)

- [ ] **Step 1: Create the test time provider** (`FixedTimeProvider.cs`)

```csharp
namespace AFK4.Platform.Api.Tests.Billing;

internal sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
```

- [ ] **Step 2: Create the interface**

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IPlanCatalogService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<SubscriptionPlanDto?> GetAsync(string planCode, CancellationToken cancellationToken);

    Task<BillingOperationResult<SubscriptionPlanDto>> CreateAsync(
        CreatePlanRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<SubscriptionPlanDto>> UpdateAsync(
        string planCode,
        UpdatePlanRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Write the failing test**

`EfPlanCatalogServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfPlanCatalogServiceTests
{
    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static CreatePlanRequest BuildCreate(string planCode = "team") =>
        new(
            PlanCode: planCode,
            Name: "Team",
            PriceMinorUnits: 500000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 2,
            MaxDevicesPerBranch: 40,
            MaxConcurrentSessions: 60,
            MaxStaffUsersPerBranch: 15,
            SortOrder: 5);

    [Fact]
    public async Task CreateAsync_PersistsPlanAndReturnsDto()
    {
        await using var db = NewContext();
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z"));
        var service = new EfPlanCatalogService(db, time);

        var result = await service.CreateAsync(BuildCreate(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("team", result.Value!.PlanCode);
        Assert.True(result.Value.IsActive);
        var stored = await db.SubscriptionPlans.SingleAsync();
        Assert.Equal(500000, stored.PriceMinorUnits);
    }

    [Fact]
    public async Task CreateAsync_DuplicatePlanCode_ReturnsConflict()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));
        await service.CreateAsync(BuildCreate(), CancellationToken.None);

        var result = await service.CreateAsync(BuildCreate(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task CreateAsync_InvalidInterval_ReturnsBadRequest()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));

        var result = await service.CreateAsync(BuildCreate() with { BillingInterval = "weekly" }, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_ChangesFieldsAndBumpsUpdatedAt()
    {
        await using var db = NewContext();
        var time = new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z"));
        var service = new EfPlanCatalogService(db, time);
        await service.CreateAsync(BuildCreate(), CancellationToken.None);
        time.Now = DateTimeOffset.Parse("2026-06-01T10:00:00Z");

        var result = await service.UpdateAsync("team", new UpdatePlanRequest(
            Name: "Team Plus",
            PriceMinorUnits: 600000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 3,
            MaxDevicesPerBranch: 40,
            MaxConcurrentSessions: 60,
            MaxStaffUsersPerBranch: 15,
            IsActive: false,
            SortOrder: 5), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("Team Plus", result.Value!.Name);
        Assert.False(result.Value.IsActive);
        var stored = await db.SubscriptionPlans.SingleAsync();
        Assert.Equal(time.Now, stored.UpdatedAtUtc);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPlan_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));

        var result = await service.UpdateAsync("ghost", new UpdatePlanRequest(
            "X", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, true, 1), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task ListAsync_ExcludesInactiveUnlessRequested()
    {
        await using var db = NewContext();
        var service = new EfPlanCatalogService(db, new FixedTimeProvider(DateTimeOffset.Parse("2026-05-31T10:00:00Z")));
        await service.CreateAsync(BuildCreate("a"), CancellationToken.None);
        await service.CreateAsync(BuildCreate("b"), CancellationToken.None);
        await service.UpdateAsync("b", new UpdatePlanRequest(
            "B", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, false, 9), CancellationToken.None);

        var active = await service.ListAsync(includeInactive: false, CancellationToken.None);
        var all = await service.ListAsync(includeInactive: true, CancellationToken.None);

        Assert.Single(active);
        Assert.Equal(2, all.Count);
    }
}
```

- [ ] **Step 4: Run the test to verify it fails to compile/fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfPlanCatalogServiceTests`
Expected: FAIL — `EfPlanCatalogService` does not exist.

- [ ] **Step 5: Implement `EfPlanCatalogService`**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfPlanCatalogService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : IPlanCatalogService
{
    private const int MaxPlanCodeLength = 64;
    private const int MaxNameLength = 160;

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.Ordinal)
    {
        BillingIntervalNames.Monthly,
        BillingIntervalNames.Yearly
    };

    public async Task<IReadOnlyList<SubscriptionPlanDto>> ListAsync(
        bool includeInactive,
        CancellationToken cancellationToken)
    {
        var query = dbContext.SubscriptionPlans.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(plan => plan.IsActive);
        }

        var plans = await query
            .OrderBy(plan => plan.SortOrder)
            .ThenBy(plan => plan.PlanCode)
            .ToListAsync(cancellationToken);
        return plans.Select(ToDto).ToList();
    }

    public async Task<SubscriptionPlanDto?> GetAsync(string planCode, CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var plan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == normalized, cancellationToken);
        return plan is null ? null : ToDto(plan);
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> CreateAsync(
        CreatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var planCode = (request.PlanCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(planCode, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits);
        if (validationError is not null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.BadRequest(validationError);
        }

        var exists = await dbContext.SubscriptionPlans.AnyAsync(plan => plan.PlanCode == planCode, cancellationToken);
        if (exists)
        {
            return BillingOperationResult<SubscriptionPlanDto>.Conflict($"Plan '{planCode}' already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var entity = new SubscriptionPlanEntity
        {
            PlanCode = planCode,
            Name = request.Name.Trim(),
            PriceMinorUnits = request.PriceMinorUnits,
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            BillingInterval = request.BillingInterval.Trim(),
            MaxBranches = request.MaxBranches,
            MaxDevicesPerBranch = request.MaxDevicesPerBranch,
            MaxConcurrentSessions = request.MaxConcurrentSessions,
            MaxStaffUsersPerBranch = request.MaxStaffUsersPerBranch,
            IsActive = true,
            SortOrder = request.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.SubscriptionPlans.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success(ToDto(entity));
    }

    public async Task<BillingOperationResult<SubscriptionPlanDto>> UpdateAsync(
        string planCode,
        UpdatePlanRequest request,
        CancellationToken cancellationToken)
    {
        var normalized = (planCode ?? string.Empty).Trim();
        var validationError = ValidateCommon(normalized, request.Name, request.CurrencyCode, request.BillingInterval, request.PriceMinorUnits);
        if (validationError is not null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.BadRequest(validationError);
        }

        var entity = await dbContext.SubscriptionPlans
            .SingleOrDefaultAsync(plan => plan.PlanCode == normalized, cancellationToken);
        if (entity is null)
        {
            return BillingOperationResult<SubscriptionPlanDto>.NotFound($"Plan '{normalized}' was not found.");
        }

        entity.Name = request.Name.Trim();
        entity.PriceMinorUnits = request.PriceMinorUnits;
        entity.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        entity.BillingInterval = request.BillingInterval.Trim();
        entity.MaxBranches = request.MaxBranches;
        entity.MaxDevicesPerBranch = request.MaxDevicesPerBranch;
        entity.MaxConcurrentSessions = request.MaxConcurrentSessions;
        entity.MaxStaffUsersPerBranch = request.MaxStaffUsersPerBranch;
        entity.IsActive = request.IsActive;
        entity.SortOrder = request.SortOrder;
        entity.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<SubscriptionPlanDto>.Success(ToDto(entity));
    }

    private static string? ValidateCommon(string planCode, string? name, string? currencyCode, string? interval, long price)
    {
        if (string.IsNullOrWhiteSpace(planCode) || planCode.Length > MaxPlanCodeLength)
        {
            return $"PlanCode is required and must be {MaxPlanCodeLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > MaxNameLength)
        {
            return $"Name is required and must be {MaxNameLength} characters or fewer.";
        }

        if (string.IsNullOrWhiteSpace(currencyCode) || currencyCode.Trim().Length != 3)
        {
            return "CurrencyCode must be a 3-letter code.";
        }

        if (string.IsNullOrWhiteSpace(interval) || !AllowedIntervals.Contains(interval.Trim()))
        {
            return $"BillingInterval must be one of: {string.Join(", ", AllowedIntervals)}.";
        }

        if (price < 0)
        {
            return "PriceMinorUnits must be non-negative.";
        }

        return null;
    }

    private static SubscriptionPlanDto ToDto(SubscriptionPlanEntity entity) =>
        new(
            PlanCode: entity.PlanCode,
            Name: entity.Name,
            PriceMinorUnits: entity.PriceMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            BillingInterval: entity.BillingInterval,
            MaxBranches: entity.MaxBranches,
            MaxDevicesPerBranch: entity.MaxDevicesPerBranch,
            MaxConcurrentSessions: entity.MaxConcurrentSessions,
            MaxStaffUsersPerBranch: entity.MaxStaffUsersPerBranch,
            IsActive: entity.IsActive,
            SortOrder: entity.SortOrder);
}
```

- [ ] **Step 6: Register in DI**

In `Program.cs`, after the platform tenancy registrations (near line 178, after `IPlatformTenantHealthService`):
```csharp
builder.Services.AddScoped<IPlanCatalogService, EfPlanCatalogService>();
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfPlanCatalogServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/IPlanCatalogService.cs src/AFK4.Platform.Api/Platform/Billing/EfPlanCatalogService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/FixedTimeProvider.cs tests/AFK4.Platform.Api.Tests/Billing/EfPlanCatalogServiceTests.cs
git commit -m "feat(platform): plan catalog service + tests"
```

---

## Task 9: Plan seed hosted service

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/BillingPlanSeedHostedService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/BillingPlanSeedHostedServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (registration)

- [ ] **Step 1: Write the failing test**

`BillingPlanSeedHostedServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class BillingPlanSeedHostedServiceTests
{
    private static ServiceProvider BuildProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddSingleton(TimeProvider.System);
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task StartAsync_SeedsThreeDefaultPlansWhenEmpty()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var codes = await db.SubscriptionPlans.Select(plan => plan.PlanCode).OrderBy(code => code).ToListAsync();
        Assert.Equal(new[] { TenantPlanCodeNames.Growth, TenantPlanCodeNames.Scale, TenantPlanCodeNames.Starter }, codes);
    }

    [Fact]
    public async Task StartAsync_DoesNotDuplicateWhenPlansExist()
    {
        var dbName = Guid.NewGuid().ToString("N");
        await using var provider = BuildProvider(dbName);
        var service = new BillingPlanSeedHostedService(provider, TimeProvider.System, NullLogger<BillingPlanSeedHostedService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal(3, await db.SubscriptionPlans.CountAsync());
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter BillingPlanSeedHostedServiceTests`
Expected: FAIL — `BillingPlanSeedHostedService` does not exist.

- [ ] **Step 3: Implement the hosted service**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class BillingPlanSeedHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    ILogger<BillingPlanSeedHostedService> logger) : IHostedService
{
    private static readonly SubscriptionPlanEntity[] DefaultPlans =
    [
        new()
        {
            PlanCode = TenantPlanCodeNames.Starter,
            Name = "Starter",
            PriceMinorUnits = 290000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 1,
            MaxDevicesPerBranch = 30,
            MaxConcurrentSessions = 40,
            MaxStaffUsersPerBranch = 10,
            IsActive = true,
            SortOrder = 1
        },
        new()
        {
            PlanCode = TenantPlanCodeNames.Growth,
            Name = "Growth",
            PriceMinorUnits = 790000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 3,
            MaxDevicesPerBranch = 60,
            MaxConcurrentSessions = 80,
            MaxStaffUsersPerBranch = 20,
            IsActive = true,
            SortOrder = 2
        },
        new()
        {
            PlanCode = TenantPlanCodeNames.Scale,
            Name = "Scale",
            PriceMinorUnits = 1990000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            MaxBranches = 10,
            MaxDevicesPerBranch = 120,
            MaxConcurrentSessions = 200,
            MaxStaffUsersPerBranch = 50,
            IsActive = true,
            SortOrder = 3
        }
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();

        if (await dbContext.SubscriptionPlans.AnyAsync(cancellationToken))
        {
            logger.LogInformation("Subscription plan catalog already populated; skipping seed.");
            return;
        }

        var now = timeProvider.GetUtcNow();
        foreach (var template in DefaultPlans)
        {
            dbContext.SubscriptionPlans.Add(new SubscriptionPlanEntity
            {
                PlanCode = template.PlanCode,
                Name = template.Name,
                PriceMinorUnits = template.PriceMinorUnits,
                CurrencyCode = template.CurrencyCode,
                BillingInterval = template.BillingInterval,
                MaxBranches = template.MaxBranches,
                MaxDevicesPerBranch = template.MaxDevicesPerBranch,
                MaxConcurrentSessions = template.MaxConcurrentSessions,
                MaxStaffUsersPerBranch = template.MaxStaffUsersPerBranch,
                IsActive = template.IsActive,
                SortOrder = template.SortOrder,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Seeded {Count} default subscription plans.", DefaultPlans.Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

- [ ] **Step 4: Register in DI**

In `Program.cs`, after the `IPlanCatalogService` registration:
```csharp
builder.Services.AddHostedService<BillingPlanSeedHostedService>();
```

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter BillingPlanSeedHostedServiceTests`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/BillingPlanSeedHostedService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/BillingPlanSeedHostedServiceTests.cs
git commit -m "feat(platform): seed default subscription plans on startup"
```

---

## Task 10: Plan catalog endpoints

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (3 endpoints)
- Test: `tests/AFK4.Platform.Api.Tests/Billing/PlatformPlanEndpointTests.cs`

> Note: `PlatformApiFactory` runs `BillingPlanSeedHostedService` at startup, so the 3 default plans already exist in endpoint tests. Account for that in assertions.

- [ ] **Step 1: Write the failing endpoint test**

`PlatformPlanEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformPlanEndpointTests
{
    [Fact]
    public async Task GetPlans_ReturnsSeededDefaults()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync("/api/platform/plans");
        var plans = await response.Content.ReadFromJsonAsync<List<SubscriptionPlanDto>>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(3, plans!.Count);
        Assert.Contains(plans, plan => plan.PlanCode == "starter");
    }

    [Fact]
    public async Task GetPlans_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/platform/plans");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostPlans_CreatesPlanAndWritesAudit()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new CreatePlanRequest(
            PlanCode: "enterprise",
            Name: "Enterprise",
            PriceMinorUnits: 4990000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 50,
            MaxDevicesPerBranch: 300,
            MaxConcurrentSessions: 500,
            MaxStaffUsersPerBranch: 200,
            SortOrder: 4);

        var response = await client.PostAsJsonAsync("/api/platform/plans", request);
        var body = await response.Content.ReadFromJsonAsync<SubscriptionPlanDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("enterprise", body!.PlanCode);
    }

    [Fact]
    public async Task PostPlans_DuplicateCode_ReturnsConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new CreatePlanRequest(
            "starter", "Dup", 1, "RUB", BillingIntervalNames.Monthly, null, null, null, null, 9);

        var response = await client.PostAsJsonAsync("/api/platform/plans", request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task PatchPlan_UpdatesPrice()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var request = new UpdatePlanRequest(
            Name: "Starter",
            PriceMinorUnits: 350000,
            CurrencyCode: "RUB",
            BillingInterval: BillingIntervalNames.Monthly,
            MaxBranches: 1,
            MaxDevicesPerBranch: 30,
            MaxConcurrentSessions: 40,
            MaxStaffUsersPerBranch: 10,
            IsActive: true,
            SortOrder: 1);

        var response = await client.PatchAsJsonAsync("/api/platform/plans/starter", request);
        var body = await response.Content.ReadFromJsonAsync<SubscriptionPlanDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(350000, body!.PriceMinorUnits);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformPlanEndpointTests`
Expected: FAIL — endpoints return 404 (not mapped).

- [ ] **Step 3: Map the endpoints**

In `Program.cs`, after the existing tenant endpoints (e.g. after the `/plan` PATCH block around line 1483), add. (Reuse the existing `WritePlatformAuditAsync` static helper and the `PlatformAdminAuthorizationService`/`IPlanCatalogService`/`IAuditRecordWriter` injected parameters, mirroring the GET-tenants handler at lines 897–938.)

```csharp
app.MapGet("/api/platform/plans", async (
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    bool? includeInactive,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var plans = await planCatalogService.ListAsync(includeInactive ?? true, cancellationToken);
    return Results.Ok(plans);
});

app.MapPost("/api/platform/plans", async (
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    IAuditRecordWriter auditRecordWriter,
    CreatePlanRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await planCatalogService.CreateAsync(request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: Guid.Empty,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.CreatePlan,
        targetType: "SubscriptionPlan",
        targetId: result.Value!.PlanCode,
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value.PlanCode, result.Value.PriceMinorUnits, result.Value.BillingInterval },
        cancellationToken);
    return Results.Ok(result.Value);
});

app.MapPatch("/api/platform/plans/{planCode}", async (
    string planCode,
    PlatformAdminAuthorizationService authorizationService,
    IPlanCatalogService planCatalogService,
    IAuditRecordWriter auditRecordWriter,
    UpdatePlanRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManagePlans);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await planCatalogService.UpdateAsync(planCode, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: Guid.Empty,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.UpdatePlan,
        targetType: "SubscriptionPlan",
        targetId: planCode,
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value!.PlanCode, result.Value.PriceMinorUnits, result.Value.IsActive },
        cancellationToken);
    return Results.Ok(result.Value);
});
```

- [ ] **Step 4: Add the `BillingResults` status-mapping helper**

Create `src/AFK4.Platform.Api/Platform/Billing/BillingResults.cs` (maps a failed `BillingOperationResult<T>` to an HTTP result; mirrors how tenant endpoints map `PlatformTenantOperationStatus`):
```csharp
using AFK4.Platform.Api.Platform.Billing;
using Microsoft.AspNetCore.Http;

namespace AFK4.Platform.Api.Platform.Billing;

public static class BillingResults
{
    public static IResult From<T>(BillingOperationResult<T> result) where T : class =>
        result.Status switch
        {
            BillingOperationStatus.NotFound => Results.NotFound(new { Error = result.Error }),
            BillingOperationStatus.Conflict => Results.Conflict(new { Error = result.Error }),
            BillingOperationStatus.BadRequest => Results.BadRequest(new { Error = result.Error }),
            _ => Results.BadRequest(new { Error = result.Error ?? "Unknown billing error." })
        };
}
```
Add `using AFK4.Platform.Api.Platform.Billing;` to the top of `Program.cs` if not already present.

- [ ] **Step 5: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformPlanEndpointTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs src/AFK4.Platform.Api/Platform/Billing/BillingResults.cs tests/AFK4.Platform.Api.Tests/Billing/PlatformPlanEndpointTests.cs
git commit -m "feat(platform): plan catalog endpoints (GET/POST/PATCH /plans)"
```

---

## Task 11: Tenant subscription service (source of truth + proration)

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/ITenantSubscriptionService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfTenantSubscriptionService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/BillingPeriod.cs` (shared period math)
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfTenantSubscriptionServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI)

- [ ] **Step 1: Create the shared period helper** (`BillingPeriod.cs`)

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public static class BillingPeriod
{
    public static DateTimeOffset Advance(DateTimeOffset from, string billingInterval) =>
        billingInterval == BillingIntervalNames.Yearly
            ? from.AddYears(1)
            : from.AddMonths(1);
}
```

- [ ] **Step 2: Create the interface**

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface ITenantSubscriptionService
{
    Task<BillingOperationResult<TenantSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<TenantSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 3: Write the failing tests**

`EfTenantSubscriptionServiceTests.cs`:
```csharp
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfTenantSubscriptionServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<Guid> SeedOrgAndPlansAsync(PlatformDbContext db, string planCode = "starter")
    {
        db.SubscriptionPlans.AddRange(
            new SubscriptionPlanEntity { PlanCode = "starter", Name = "Starter", PriceMinorUnits = 290000, CurrencyCode = "RUB", BillingInterval = "monthly", MaxBranches = 1, MaxDevicesPerBranch = 30, MaxConcurrentSessions = 40, MaxStaffUsersPerBranch = 10, IsActive = true, SortOrder = 1, CreatedAtUtc = Now, UpdatedAtUtc = Now },
            new SubscriptionPlanEntity { PlanCode = "scale", Name = "Scale", PriceMinorUnits = 1990000, CurrencyCode = "RUB", BillingInterval = "monthly", MaxBranches = 10, MaxDevicesPerBranch = 120, MaxConcurrentSessions = 200, MaxStaffUsersPerBranch = 50, IsActive = true, SortOrder = 3, CreatedAtUtc = Now, UpdatedAtUtc = Now });
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId,
            Slug = "demo",
            Name = "Demo",
            Status = TenantStatusNames.Active,
            PlanCode = planCode,
            SubscriptionStatus = SubscriptionStatusNames.Active,
            LimitsJson = "{}",
            CreatedAtUtc = Now,
            UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    [Fact]
    public async Task GetAsync_LazilyCreatesSubscriptionFromCatalogPlan()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfTenantSubscriptionService(db, new FixedTimeProvider(Now));

        var result = await service.GetAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("starter", result.Value!.PlanCode);
        Assert.Equal(290000, result.Value.AmountMinorUnits);
        Assert.Equal(Now, result.Value.CurrentPeriodStartUtc);
        Assert.Equal(Now.AddMonths(1), result.Value.CurrentPeriodEndUtc);
        Assert.Equal(1, await db.TenantSubscriptions.CountAsync());
    }

    [Fact]
    public async Task GetAsync_UnknownOrg_ReturnsNotFound()
    {
        await using var db = NewContext();
        var service = new EfTenantSubscriptionService(db, new FixedTimeProvider(Now));

        var result = await service.GetAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_UpgradeMidCycle_IssuesProrationInvoiceAndSyncsOrg()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var time = new FixedTimeProvider(Now);
        var service = new EfTenantSubscriptionService(db, time);
        await service.GetAsync(orgId, CancellationToken.None); // create subscription (period 05-31 -> 06-30)

        // Move 15 days into the period, then upgrade starter(290000) -> scale(1990000).
        time.Now = Now.AddDays(15);
        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "scale", BillingInterval: null, Status: null, CancelAtPeriodEnd: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal("scale", result.Value!.PlanCode);
        Assert.Equal(1990000, result.Value.AmountMinorUnits);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceKindNames.Proration, invoice.Kind);
        Assert.True(invoice.AmountMinorUnits > 0);

        var org = await db.Organizations.SingleAsync(o => o.OrganizationId == orgId);
        Assert.Equal("scale", org.PlanCode);
        var limits = JsonSerializer.Deserialize<TenantLimitsDto>(org.LimitsJson)!;
        Assert.Equal(10, limits.MaxBranches);
    }

    [Fact]
    public async Task UpdateAsync_Downgrade_DoesNotIssueInvoice()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "scale");
        var time = new FixedTimeProvider(Now);
        var service = new EfTenantSubscriptionService(db, time);
        await service.GetAsync(orgId, CancellationToken.None);

        time.Now = Now.AddDays(15);
        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "starter", BillingInterval: null, Status: null, CancelAtPeriodEnd: null), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(290000, result.Value!.AmountMinorUnits);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task UpdateAsync_StatusChange_SyncsOrgSubscriptionStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfTenantSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: null, BillingInterval: null, Status: SubscriptionStatusNames.PastDue, CancelAtPeriodEnd: true), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.Value!.CancelAtPeriodEnd);
        var org = await db.Organizations.SingleAsync(o => o.OrganizationId == orgId);
        Assert.Equal(SubscriptionStatusNames.PastDue, org.SubscriptionStatus);
    }

    [Fact]
    public async Task UpdateAsync_UnknownPlan_ReturnsBadRequest()
    {
        await using var db = NewContext();
        var orgId = await SeedOrgAndPlansAsync(db, "starter");
        var service = new EfTenantSubscriptionService(db, new FixedTimeProvider(Now));
        await service.GetAsync(orgId, CancellationToken.None);

        var result = await service.UpdateAsync(orgId, new UpdateSubscriptionRequest(
            PlanCode: "ghost", BillingInterval: null, Status: null, CancelAtPeriodEnd: null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.BadRequest, result.Status);
    }
}
```

- [ ] **Step 4: Run to verify the tests fail**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfTenantSubscriptionServiceTests`
Expected: FAIL — `EfTenantSubscriptionService` does not exist.

- [ ] **Step 5: Implement `EfTenantSubscriptionService`**

```csharp
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfTenantSubscriptionService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider) : ITenantSubscriptionService
{
    private static readonly HashSet<string> AllowedStatuses = new(StringComparer.Ordinal)
    {
        SubscriptionStatusNames.Trial,
        SubscriptionStatusNames.Active,
        SubscriptionStatusNames.PastDue,
        SubscriptionStatusNames.Cancelled
    };

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.Ordinal)
    {
        BillingIntervalNames.Monthly,
        BillingIntervalNames.Yearly
    };

    public async Task<BillingOperationResult<TenantSubscriptionDto>> GetAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<TenantSubscriptionDto>.NotFound("Tenant was not found.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        return BillingOperationResult<TenantSubscriptionDto>.Success(ToDto(subscription));
    }

    public async Task<BillingOperationResult<TenantSubscriptionDto>> UpdateAsync(
        Guid organizationId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        var org = await dbContext.Organizations
            .SingleOrDefaultAsync(o => o.OrganizationId == organizationId, cancellationToken);
        if (org is null)
        {
            return BillingOperationResult<TenantSubscriptionDto>.NotFound("Tenant was not found.");
        }

        if (request.Status is not null && !AllowedStatuses.Contains(request.Status.Trim()))
        {
            return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                $"Status must be one of: {string.Join(", ", AllowedStatuses)}.");
        }

        if (request.BillingInterval is not null && !AllowedIntervals.Contains(request.BillingInterval.Trim()))
        {
            return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                $"BillingInterval must be one of: {string.Join(", ", AllowedIntervals)}.");
        }

        var subscription = await EnsureSubscriptionAsync(org, cancellationToken);
        var now = timeProvider.GetUtcNow();

        // Plan change (with proration on upgrade).
        if (request.PlanCode is not null && request.PlanCode.Trim() != subscription.PlanCode)
        {
            var newPlan = await dbContext.SubscriptionPlans
                .SingleOrDefaultAsync(plan => plan.PlanCode == request.PlanCode.Trim(), cancellationToken);
            if (newPlan is null)
            {
                return BillingOperationResult<TenantSubscriptionDto>.BadRequest(
                    $"Plan '{request.PlanCode.Trim()}' was not found.");
            }

            var newInterval = request.BillingInterval?.Trim() ?? newPlan.BillingInterval;
            var proration = ComputeProration(
                subscription.AmountMinorUnits,
                newPlan.PriceMinorUnits,
                subscription.CurrentPeriodStartUtc,
                subscription.CurrentPeriodEndUtc,
                now);
            if (proration > 0)
            {
                dbContext.Invoices.Add(new InvoiceEntity
                {
                    InvoiceId = Guid.NewGuid(),
                    OrganizationId = org.OrganizationId,
                    Number = await NextInvoiceNumberAsync(cancellationToken),
                    Kind = InvoiceKindNames.Proration,
                    PeriodStartUtc = now,
                    PeriodEndUtc = subscription.CurrentPeriodEndUtc,
                    IssuedAtUtc = now,
                    DueAtUtc = now.AddDays(7),
                    AmountMinorUnits = proration,
                    CurrencyCode = newPlan.CurrencyCode,
                    Status = InvoiceStatusNames.Issued,
                    Description = $"Proration: {subscription.PlanCode} → {newPlan.PlanCode}",
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now
                });
            }

            subscription.PlanCode = newPlan.PlanCode;
            subscription.AmountMinorUnits = newPlan.PriceMinorUnits;
            subscription.CurrencyCode = newPlan.CurrencyCode;
            subscription.BillingInterval = newInterval;

            // Copy the plan's limits into the org's denormalized limits.
            org.LimitsJson = JsonSerializer.Serialize(new TenantLimitsDto(
                newPlan.MaxBranches,
                newPlan.MaxDevicesPerBranch,
                newPlan.MaxConcurrentSessions,
                newPlan.MaxStaffUsersPerBranch));
            org.PlanCode = newPlan.PlanCode;
        }
        else if (request.BillingInterval is not null)
        {
            subscription.BillingInterval = request.BillingInterval.Trim();
        }

        if (request.Status is not null)
        {
            subscription.Status = request.Status.Trim();
            org.SubscriptionStatus = subscription.Status;
        }

        if (request.CancelAtPeriodEnd is not null)
        {
            subscription.CancelAtPeriodEnd = request.CancelAtPeriodEnd.Value;
        }

        subscription.UpdatedAtUtc = now;
        org.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<TenantSubscriptionDto>.Success(ToDto(subscription));
    }

    private async Task<TenantSubscriptionEntity> EnsureSubscriptionAsync(
        OrganizationEntity org,
        CancellationToken cancellationToken)
    {
        var existing = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(subscription => subscription.OrganizationId == org.OrganizationId, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var plan = await dbContext.SubscriptionPlans
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == org.PlanCode, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var interval = plan?.BillingInterval ?? BillingIntervalNames.Monthly;
        var subscription = new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = org.OrganizationId,
            PlanCode = org.PlanCode,
            Status = org.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = BillingPeriod.Advance(now, interval),
            NextInvoiceUtc = BillingPeriod.Advance(now, interval),
            AmountMinorUnits = plan?.PriceMinorUnits ?? 0,
            CurrencyCode = plan?.CurrencyCode ?? "RUB",
            BillingInterval = interval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.TenantSubscriptions.Add(subscription);
        await dbContext.SaveChangesAsync(cancellationToken);
        return subscription;
    }

    private async Task<int> NextInvoiceNumberAsync(CancellationToken cancellationToken)
    {
        var max = await dbContext.Invoices
            .Select(invoice => (int?)invoice.Number)
            .MaxAsync(cancellationToken);
        return (max ?? 0) + 1;
    }

    internal static long ComputeProration(
        long oldAmount,
        long newAmount,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        DateTimeOffset now)
    {
        var totalDays = (periodEnd - periodStart).TotalDays;
        if (totalDays <= 0)
        {
            return 0;
        }

        var remainingDays = Math.Max(0, (periodEnd - now).TotalDays);
        var oldDaily = oldAmount / totalDays;
        var newDaily = newAmount / totalDays;
        return (long)Math.Round((newDaily - oldDaily) * remainingDays, MidpointRounding.AwayFromZero);
    }

    private static TenantSubscriptionDto ToDto(TenantSubscriptionEntity entity) =>
        new(
            TenantSubscriptionId: entity.TenantSubscriptionId,
            OrganizationId: entity.OrganizationId,
            PlanCode: entity.PlanCode,
            Status: entity.Status,
            CurrentPeriodStartUtc: entity.CurrentPeriodStartUtc,
            CurrentPeriodEndUtc: entity.CurrentPeriodEndUtc,
            NextInvoiceUtc: entity.NextInvoiceUtc,
            AmountMinorUnits: entity.AmountMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            BillingInterval: entity.BillingInterval,
            CancelAtPeriodEnd: entity.CancelAtPeriodEnd,
            CreatedAtUtc: entity.CreatedAtUtc,
            UpdatedAtUtc: entity.UpdatedAtUtc);
}
```

- [ ] **Step 6: Register in DI**

In `Program.cs`, after `IPlanCatalogService`:
```csharp
builder.Services.AddScoped<ITenantSubscriptionService, EfTenantSubscriptionService>();
```

- [ ] **Step 7: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfTenantSubscriptionServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/ITenantSubscriptionService.cs src/AFK4.Platform.Api/Platform/Billing/EfTenantSubscriptionService.cs src/AFK4.Platform.Api/Platform/Billing/BillingPeriod.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/EfTenantSubscriptionServiceTests.cs
git commit -m "feat(platform): tenant subscription service with proration + org sync"
```

---

## Task 12: Seed a subscription on tenant creation

**Files:**
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfTenantSubscriptionServiceTests.cs` (add one test) — or a new test in the tenancy test file. Use the existing `PlatformTenantEndpointTests` harness.

> New tenants should get a `TenantSubscriptionEntity` immediately (not only lazily), so the background job bills them from day one. We add a subscription row inside `CreateAsync` after the org/branch are persisted, deriving amount/interval from the catalog plan if present.

- [ ] **Step 1: Write the failing test** (add to `tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs`)

```csharp
    [Fact]
    public async Task PostTenants_SeedsTenantSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.PostAsJsonAsync("/api/platform/tenants", BuildCreateTenantRequest());
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var subscription = await dbContext.TenantSubscriptions
            .SingleAsync(s => s.OrganizationId == body!.Tenant.OrganizationId);
        Assert.Equal("starter", subscription.PlanCode);
        Assert.Equal(290000, subscription.AmountMinorUnits); // from seeded starter plan
    }
```
(Ensure the test file has `using AFK4.Platform.Api.Data;`, `using Microsoft.Extensions.DependencyInjection;`, `using Microsoft.EntityFrameworkCore;` — add any missing.)

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PostTenants_SeedsTenantSubscription`
Expected: FAIL — no subscription row (`SingleAsync` throws).

- [ ] **Step 3: Add subscription seeding to `CreateAsync`**

In `EfPlatformTenantService.cs`, in `CreateAsync`, after `dbContext.OwnerInvites.Add(invite);` and before `await dbContext.SaveChangesAsync(cancellationToken);`, add a subscription built from the catalog plan. First load the plan (just above the `dbContext.Organizations.Add(...)` is fine, but it needs the org's plan code/limits already chosen):

```csharp
        var catalogPlan = await dbContext.SubscriptionPlans
            .AsNoTracking()
            .SingleOrDefaultAsync(plan => plan.PlanCode == organization.PlanCode, cancellationToken);
        var subscriptionInterval = catalogPlan?.BillingInterval ?? "monthly";
        dbContext.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = organization.OrganizationId,
            PlanCode = organization.PlanCode,
            Status = organization.SubscriptionStatus,
            CurrentPeriodStartUtc = now,
            CurrentPeriodEndUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            NextInvoiceUtc = subscriptionInterval == "yearly" ? now.AddYears(1) : now.AddMonths(1),
            AmountMinorUnits = catalogPlan?.PriceMinorUnits ?? 0,
            CurrencyCode = catalogPlan?.CurrencyCode ?? "RUB",
            BillingInterval = subscriptionInterval,
            CancelAtPeriodEnd = false,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
```
(`BillingIntervalNames` lives in `AFK4.Shared.Contracts.Platform.Billing`; the literals `"monthly"`/`"yearly"` are used inline here to avoid adding a new using, matching the existing string-literal style in this file. Alternatively add `using AFK4.Shared.Contracts.Platform.Billing;` and use the constants.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PostTenants_SeedsTenantSubscription`
Expected: PASS.

- [ ] **Step 5: Run the full tenant endpoint suite to confirm no regressions**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformTenantEndpointTests`
Expected: PASS (all existing + the new test).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformTenantService.cs tests/AFK4.Platform.Api.Tests/Platform/PlatformTenantEndpointTests.cs
git commit -m "feat(platform): seed tenant subscription on creation"
```

---

## Task 13: Subscription endpoints (GET / PATCH)

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (2 endpoints)
- Test: `tests/AFK4.Platform.Api.Tests/Billing/PlatformSubscriptionEndpointTests.cs`

- [ ] **Step 1: Write the failing test**

`PlatformSubscriptionEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformSubscriptionEndpointTests
{
    private static async Task<Guid> CreateTenantAsync(PlatformApiFactory factory, HttpClient client)
    {
        var request = new CreateTenantRequest(
            OrganizationSlug: "sub-club",
            OrganizationName: "Sub Club",
            BranchSlug: "main",
            BranchName: "Main",
            BranchCity: "Dushanbe",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Active,
            Limits: new TenantLimitsDto(1, 30, 40, 10),
            OwnerUserName: "owner@sub-club.test",
            OwnerDisplayName: "Owner",
            OwnerInviteLifetime: TimeSpan.FromDays(7));
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        return body!.Tenant.OrganizationId;
    }

    [Fact]
    public async Task GetSubscription_ReturnsSeededSubscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{orgId}/subscription");
        var body = await response.Content.ReadFromJsonAsync<TenantSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("starter", body!.PlanCode);
    }

    [Fact]
    public async Task PatchSubscription_ChangesPlan()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(factory, client);

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/tenants/{orgId}/subscription",
            new UpdateSubscriptionRequest("scale", null, null, null));
        var body = await response.Content.ReadFromJsonAsync<TenantSubscriptionDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("scale", body!.PlanCode);
    }

    [Fact]
    public async Task GetSubscription_UnknownTenant_ReturnsNotFound()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetSubscription_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid()}/subscription");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformSubscriptionEndpointTests`
Expected: FAIL — endpoints not mapped (404).

- [ ] **Step 3: Map the endpoints** (in `Program.cs`, after the plan endpoints)

```csharp
app.MapGet("/api/platform/tenants/{organizationId:guid}/subscription", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await subscriptionService.GetAsync(organizationId, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapPatch("/api/platform/tenants/{organizationId:guid}/subscription", async (
    Guid organizationId,
    PlatformAdminAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    IAuditRecordWriter auditRecordWriter,
    UpdateSubscriptionRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageSubscriptions);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await subscriptionService.UpdateAsync(organizationId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter,
        organizationId: organizationId,
        actorPlatformAdminUserId: authorization.PlatformAdminContext!.PlatformAdminUserId,
        action: AuditActionNames.UpdateSubscription,
        targetType: "TenantSubscription",
        targetId: organizationId.ToString("D"),
        outcome: AuditOutcome.Succeeded,
        details: new { result.Value!.PlanCode, result.Value.Status, result.Value.CancelAtPeriodEnd },
        cancellationToken);
    return Results.Ok(result.Value);
});
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformSubscriptionEndpointTests`
Expected: PASS (4 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/PlatformSubscriptionEndpointTests.cs
git commit -m "feat(platform): tenant subscription endpoints (GET/PATCH)"
```

---

## Task 14: Invoice generation runner (core logic)

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceGenerationRunner.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceGenerationRunner.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceGenerationRunnerTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI)

- [ ] **Step 1: Create the interface**

```csharp
using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IInvoiceGenerationRunner
{
    /// <summary>
    /// Issues invoices for every active subscription whose NextInvoiceUtc is due, advances those
    /// subscriptions, and flips overdue invoices. Returns the number of invoices issued.
    /// </summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a subscription invoice for the subscription's current period (if not already issued)
    /// and advances the period. Returns the issued invoice, or null if one already existed.
    /// Caller is responsible for SaveChanges.
    /// </summary>
    Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        TenantSubscriptionEntity subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the failing tests**

`EfInvoiceGenerationRunnerTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfInvoiceGenerationRunnerTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfInvoiceGenerationRunner NewRunner(PlatformDbContext db) =>
        new(db, Options.Create(new BillingOptions()));

    private static async Task<TenantSubscriptionEntity> SeedActiveDueSubscriptionAsync(PlatformDbContext db)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = TenantStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Start, UpdatedAtUtc = Start
        });
        var subscription = new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = orgId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
            CurrentPeriodStartUtc = Start,
            CurrentPeriodEndUtc = Start.AddMonths(1),
            NextInvoiceUtc = Start.AddMonths(1),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly,
            CreatedAtUtc = Start,
            UpdatedAtUtc = Start
        };
        db.TenantSubscriptions.Add(subscription);
        await db.SaveChangesAsync();
        return subscription;
    }

    [Fact]
    public async Task RunAsync_DueSubscription_IssuesInvoiceAndAdvancesPeriod()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        Assert.Equal(1, issued);
        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceKindNames.Subscription, invoice.Kind);
        Assert.Equal(290000, invoice.AmountMinorUnits);
        Assert.Equal(1, invoice.Number);
        var reloaded = await db.TenantSubscriptions.SingleAsync();
        Assert.Equal(Start.AddMonths(1), reloaded.CurrentPeriodStartUtc);
        Assert.Equal(Start.AddMonths(2), reloaded.CurrentPeriodEndUtc);
    }

    [Fact]
    public async Task RunAsync_NotYetDue_IssuesNothing()
    {
        await using var db = NewContext();
        await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddDays(5), CancellationToken.None);

        Assert.Equal(0, issued);
        Assert.Equal(0, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_RunTwiceSamePeriod_DoesNotDoubleIssue()
    {
        await using var db = NewContext();
        await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);

        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);
        // Second run at the same instant: period already advanced, next invoice not yet due.
        var second = await runner.RunAsync(Start.AddMonths(1), CancellationToken.None);

        Assert.Equal(0, second);
        Assert.Equal(1, await db.Invoices.CountAsync());
    }

    [Fact]
    public async Task RunAsync_FlipsIssuedInvoicesToOverdueAfterDueDate()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        var runner = NewRunner(db);
        await runner.RunAsync(Start.AddMonths(1), CancellationToken.None); // issues invoice due +7 days

        await runner.RunAsync(Start.AddMonths(1).AddDays(8), CancellationToken.None);

        var invoice = await db.Invoices.SingleAsync();
        Assert.Equal(InvoiceStatusNames.Overdue, invoice.Status);
    }

    [Fact]
    public async Task RunAsync_CancelledSubscription_IsSkipped()
    {
        await using var db = NewContext();
        var subscription = await SeedActiveDueSubscriptionAsync(db);
        subscription.Status = SubscriptionStatusNames.Cancelled;
        await db.SaveChangesAsync();
        var runner = NewRunner(db);

        var issued = await runner.RunAsync(Start.AddMonths(2), CancellationToken.None);

        Assert.Equal(0, issued);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfInvoiceGenerationRunnerTests`
Expected: FAIL — `EfInvoiceGenerationRunner` does not exist.

- [ ] **Step 4: Implement `EfInvoiceGenerationRunner`**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceGenerationRunner(
    PlatformDbContext dbContext,
    IOptions<BillingOptions> options) : IInvoiceGenerationRunner
{
    private readonly BillingOptions options = options.Value;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var dueSubscriptions = await dbContext.TenantSubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatusNames.Active &&
                subscription.NextInvoiceUtc != null &&
                subscription.NextInvoiceUtc <= now)
            .ToListAsync(cancellationToken);

        var issued = 0;
        foreach (var subscription in dueSubscriptions)
        {
            var invoice = await GenerateForSubscriptionAsync(subscription, now, cancellationToken);
            if (invoice is not null)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                issued++;
            }
        }

        var overdue = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc < now)
            .ToListAsync(cancellationToken);
        foreach (var invoice in overdue)
        {
            invoice.Status = InvoiceStatusNames.Overdue;
            invoice.UpdatedAtUtc = now;
        }

        if (overdue.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return issued;
    }

    public async Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        TenantSubscriptionEntity subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var alreadyIssued = await dbContext.Invoices.AnyAsync(invoice =>
            invoice.OrganizationId == subscription.OrganizationId &&
            invoice.Kind == InvoiceKindNames.Subscription &&
            invoice.PeriodStartUtc == subscription.CurrentPeriodStartUtc &&
            invoice.Status != InvoiceStatusNames.Void,
            cancellationToken);
        if (alreadyIssued)
        {
            return null;
        }

        var number = ((await dbContext.Invoices
            .Select(invoice => (int?)invoice.Number)
            .MaxAsync(cancellationToken)) ?? 0) + 1;

        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = subscription.OrganizationId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = subscription.CurrentPeriodStartUtc,
            PeriodEndUtc = subscription.CurrentPeriodEndUtc,
            IssuedAtUtc = now,
            DueAtUtc = now.Add(options.InvoiceDueAfter),
            AmountMinorUnits = subscription.AmountMinorUnits,
            CurrencyCode = subscription.CurrencyCode,
            Status = InvoiceStatusNames.Issued,
            Description = $"Subscription {subscription.PlanCode} " +
                $"({subscription.CurrentPeriodStartUtc:yyyy-MM-dd} – {subscription.CurrentPeriodEndUtc:yyyy-MM-dd})",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Invoices.Add(invoice);

        subscription.CurrentPeriodStartUtc = subscription.CurrentPeriodEndUtc;
        subscription.CurrentPeriodEndUtc = BillingPeriod.Advance(subscription.CurrentPeriodEndUtc, subscription.BillingInterval);
        subscription.NextInvoiceUtc = subscription.CurrentPeriodEndUtc;
        subscription.UpdatedAtUtc = now;
        return invoice;
    }
}
```

- [ ] **Step 5: Register in DI**

In `Program.cs`, after `ITenantSubscriptionService`:
```csharp
builder.Services.AddScoped<IInvoiceGenerationRunner, EfInvoiceGenerationRunner>();
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection(BillingOptions.ConfigurationSection));
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfInvoiceGenerationRunnerTests`
Expected: PASS (5 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/IInvoiceGenerationRunner.cs src/AFK4.Platform.Api/Platform/Billing/EfInvoiceGenerationRunner.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceGenerationRunnerTests.cs
git commit -m "feat(platform): invoice generation runner (issue, advance, overdue)"
```

---

## Task 15: Invoice service (list / generate / mark-paid / void)

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Billing/EfInvoiceService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (DI)

- [ ] **Step 1: Create the interface**

```csharp
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IInvoiceService
{
    Task<BillingOperationResult<IReadOnlyList<InvoiceDto>>> ListForTenantAsync(
        Guid organizationId,
        string? status,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> GenerateAsync(
        Guid organizationId,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> MarkPaidAsync(
        Guid invoiceId,
        MarkInvoicePaidRequest request,
        CancellationToken cancellationToken);

    Task<BillingOperationResult<InvoiceDto>> VoidAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 2: Write the failing tests**

`EfInvoiceServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class EfInvoiceServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-31T00:00:00Z");

    private static PlatformDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static EfInvoiceService NewService(PlatformDbContext db, TimeProvider time) =>
        new(db, new EfInvoiceGenerationRunner(db, Options.Create(new BillingOptions())), time);

    private static async Task<Guid> SeedTenantWithSubscriptionAsync(PlatformDbContext db)
    {
        var orgId = Guid.NewGuid();
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = orgId, Slug = "o", Name = "O", Status = TenantStatusNames.Active,
            PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
            CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
            Status = SubscriptionStatusNames.Active, CurrentPeriodStartUtc = Now, CurrentPeriodEndUtc = Now.AddMonths(1),
            NextInvoiceUtc = Now.AddMonths(1), AmountMinorUnits = 290000, CurrencyCode = "RUB",
            BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = Now, UpdatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        return orgId;
    }

    [Fact]
    public async Task GenerateAsync_IssuesInvoiceForCurrentPeriod()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));

        var result = await service.GenerateAsync(orgId, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(290000, result.Value!.AmountMinorUnits);
        Assert.Equal(InvoiceStatusNames.Issued, result.Value.Status);
    }

    [Fact]
    public async Task GenerateAsync_AlreadyIssuedForPeriod_ReturnsConflict()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        await service.GenerateAsync(orgId, CancellationToken.None);

        // Subscription period advanced; generating again issues the NEXT period's invoice (not a conflict).
        // To assert idempotency, regenerate against the same period by resetting the subscription.
        var subscription = await db.TenantSubscriptions.SingleAsync();
        subscription.CurrentPeriodStartUtc = Now;
        subscription.CurrentPeriodEndUtc = Now.AddMonths(1);
        await db.SaveChangesAsync();

        var result = await service.GenerateAsync(orgId, CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task ListForTenantAsync_FiltersByStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        await service.GenerateAsync(orgId, CancellationToken.None);

        var issued = await service.ListForTenantAsync(orgId, InvoiceStatusNames.Issued, CancellationToken.None);
        var paid = await service.ListForTenantAsync(orgId, InvoiceStatusNames.Paid, CancellationToken.None);

        Assert.Single(issued.Value!);
        Assert.Empty(paid.Value!);
    }

    [Fact]
    public async Task MarkPaidAsync_SetsPaidStatusAndTimestamp()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var time = new FixedTimeProvider(Now.AddDays(2));
        var service = NewService(db, time);
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        time.Now = Now.AddDays(3);
        var result = await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest("ref-1"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Paid, result.Value!.Status);
        Assert.Equal(time.Now, result.Value.PaidAtUtc);
    }

    [Fact]
    public async Task MarkPaidAsync_AlreadyPaid_ReturnsConflict()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var service = NewService(db, new FixedTimeProvider(Now.AddDays(2)));
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);
        await service.MarkPaidAsync(generated.Value!.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        var result = await service.MarkPaidAsync(generated.Value.InvoiceId, new MarkInvoicePaidRequest(null), CancellationToken.None);

        Assert.Equal(BillingOperationStatus.Conflict, result.Status);
    }

    [Fact]
    public async Task VoidAsync_RequiresReasonAndSetsVoidStatus()
    {
        await using var db = NewContext();
        var orgId = await SeedTenantWithSubscriptionAsync(db);
        var time = new FixedTimeProvider(Now.AddDays(2));
        var service = NewService(db, time);
        var generated = await service.GenerateAsync(orgId, CancellationToken.None);

        var missingReason = await service.VoidAsync(generated.Value!.InvoiceId, new VoidInvoiceRequest("  "), CancellationToken.None);
        Assert.Equal(BillingOperationStatus.BadRequest, missingReason.Status);

        var result = await service.VoidAsync(generated.Value.InvoiceId, new VoidInvoiceRequest("duplicate"), CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(InvoiceStatusNames.Void, result.Value!.Status);
        Assert.Equal("duplicate", result.Value.VoidReason);
    }
}
```

- [ ] **Step 3: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfInvoiceServiceTests`
Expected: FAIL — `EfInvoiceService` does not exist.

- [ ] **Step 4: Implement `EfInvoiceService`**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceService(
    PlatformDbContext dbContext,
    IInvoiceGenerationRunner generationRunner,
    TimeProvider timeProvider) : IInvoiceService
{
    private const int MaxVoidReasonLength = 512;

    private static readonly HashSet<string> AllowedStatusFilters = new(StringComparer.Ordinal)
    {
        InvoiceStatusNames.Issued,
        InvoiceStatusNames.Paid,
        InvoiceStatusNames.Void,
        InvoiceStatusNames.Overdue
    };

    public async Task<BillingOperationResult<IReadOnlyList<InvoiceDto>>> ListForTenantAsync(
        Guid organizationId,
        string? status,
        CancellationToken cancellationToken)
    {
        var orgExists = await dbContext.Organizations
            .AnyAsync(org => org.OrganizationId == organizationId, cancellationToken);
        if (!orgExists)
        {
            return BillingOperationResult<IReadOnlyList<InvoiceDto>>.NotFound("Tenant was not found.");
        }

        var query = dbContext.Invoices.AsNoTracking()
            .Where(invoice => invoice.OrganizationId == organizationId);
        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalized = status.Trim();
            if (!AllowedStatusFilters.Contains(normalized))
            {
                return BillingOperationResult<IReadOnlyList<InvoiceDto>>.BadRequest(
                    $"status must be one of: {string.Join(", ", AllowedStatusFilters)}.");
            }

            query = query.Where(invoice => invoice.Status == normalized);
        }

        var invoices = await query
            .OrderByDescending(invoice => invoice.Number)
            .ToListAsync(cancellationToken);
        IReadOnlyList<InvoiceDto> dtos = invoices.Select(ToDto).ToList();
        return BillingOperationResult<IReadOnlyList<InvoiceDto>>.Success(dtos);
    }

    public async Task<BillingOperationResult<InvoiceDto>> GenerateAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        var subscription = await dbContext.TenantSubscriptions
            .SingleOrDefaultAsync(s => s.OrganizationId == organizationId, cancellationToken);
        if (subscription is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound(
                "Tenant has no subscription. Open the subscription first to create one.");
        }

        var now = timeProvider.GetUtcNow();
        var invoice = await generationRunner.GenerateForSubscriptionAsync(subscription, now, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.Conflict(
                "An invoice already exists for the current period.");
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<InvoiceDto>> MarkPaidAsync(
        Guid invoiceId,
        MarkInvoicePaidRequest request,
        CancellationToken cancellationToken)
    {
        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Invoice was not found.");
        }

        if (invoice.Status == InvoiceStatusNames.Paid)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("Invoice is already paid.");
        }

        if (invoice.Status == InvoiceStatusNames.Void)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("A voided invoice cannot be marked paid.");
        }

        var now = timeProvider.GetUtcNow();
        invoice.Status = InvoiceStatusNames.Paid;
        invoice.PaidAtUtc = now;
        invoice.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    public async Task<BillingOperationResult<InvoiceDto>> VoidAsync(
        Guid invoiceId,
        VoidInvoiceRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BillingOperationResult<InvoiceDto>.BadRequest("Reason is required to void an invoice.");
        }

        if (request.Reason.Trim().Length > MaxVoidReasonLength)
        {
            return BillingOperationResult<InvoiceDto>.BadRequest(
                $"Reason must contain {MaxVoidReasonLength} characters or fewer.");
        }

        var invoice = await dbContext.Invoices
            .SingleOrDefaultAsync(candidate => candidate.InvoiceId == invoiceId, cancellationToken);
        if (invoice is null)
        {
            return BillingOperationResult<InvoiceDto>.NotFound("Invoice was not found.");
        }

        if (invoice.Status == InvoiceStatusNames.Paid)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("A paid invoice cannot be voided.");
        }

        if (invoice.Status == InvoiceStatusNames.Void)
        {
            return BillingOperationResult<InvoiceDto>.Conflict("Invoice is already void.");
        }

        var now = timeProvider.GetUtcNow();
        invoice.Status = InvoiceStatusNames.Void;
        invoice.VoidedAtUtc = now;
        invoice.VoidReason = request.Reason.Trim();
        invoice.UpdatedAtUtc = now;
        await dbContext.SaveChangesAsync(cancellationToken);
        return BillingOperationResult<InvoiceDto>.Success(ToDto(invoice));
    }

    private static InvoiceDto ToDto(InvoiceEntity entity) =>
        new(
            InvoiceId: entity.InvoiceId,
            OrganizationId: entity.OrganizationId,
            Number: entity.Number,
            Kind: entity.Kind,
            PeriodStartUtc: entity.PeriodStartUtc,
            PeriodEndUtc: entity.PeriodEndUtc,
            IssuedAtUtc: entity.IssuedAtUtc,
            DueAtUtc: entity.DueAtUtc,
            AmountMinorUnits: entity.AmountMinorUnits,
            CurrencyCode: entity.CurrencyCode,
            Status: entity.Status,
            PaidAtUtc: entity.PaidAtUtc,
            VoidedAtUtc: entity.VoidedAtUtc,
            VoidReason: entity.VoidReason,
            Description: entity.Description);
}
```

- [ ] **Step 5: Register in DI**

In `Program.cs`, after `IInvoiceGenerationRunner`:
```csharp
builder.Services.AddScoped<IInvoiceService, EfInvoiceService>();
```

- [ ] **Step 6: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfInvoiceServiceTests`
Expected: PASS (6 tests).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/IInvoiceService.cs src/AFK4.Platform.Api/Platform/Billing/EfInvoiceService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/EfInvoiceServiceTests.cs
git commit -m "feat(platform): invoice service (list/generate/mark-paid/void)"
```

---

## Task 16: Invoice generation hosted service (background job)

**Files:**
- Create: `src/AFK4.Platform.Api/Platform/Billing/InvoiceGenerationHostedService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Billing/InvoiceGenerationHostedServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (registration)

> The hosted service is a thin loop around `IInvoiceGenerationRunner.RunAsync`. The runner is already covered by Task 14; this test only confirms one tick resolves a scope and calls the runner.

- [ ] **Step 1: Implement the hosted service**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class InvoiceGenerationHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<BillingOptions> options,
    ILogger<InvoiceGenerationHostedService> logger) : BackgroundService
{
    private readonly BillingOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Tick once immediately, then on the configured interval.
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Invoice generation tick failed.");
            }

            try
            {
                await Task.Delay(options.GenerationInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<IInvoiceGenerationRunner>();
        var now = timeProvider.GetUtcNow();
        var issued = await runner.RunAsync(now, cancellationToken);
        if (issued > 0)
        {
            logger.LogInformation("Invoice generation tick issued {Count} invoice(s).", issued);
        }
    }
}
```

- [ ] **Step 2: Write the test**

`InvoiceGenerationHostedServiceTests.cs`:
```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class InvoiceGenerationHostedServiceTests
{
    [Fact]
    public async Task ExecuteAsync_FirstTick_IssuesDueInvoice()
    {
        var dbName = Guid.NewGuid().ToString("N");
        var start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

        var services = new ServiceCollection();
        services.AddDbContext<PlatformDbContext>(options => options.UseInMemoryDatabase(dbName));
        services.AddScoped<IInvoiceGenerationRunner, EfInvoiceGenerationRunner>();
        services.Configure<BillingOptions>(options => options.GenerationInterval = TimeSpan.FromHours(1));
        await using var provider = services.BuildServiceProvider();

        await using (var scope = provider.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var orgId = Guid.NewGuid();
            db.Organizations.Add(new OrganizationEntity
            {
                OrganizationId = orgId, Slug = "o", Name = "O", Status = TenantStatusNames.Active,
                PlanCode = "starter", SubscriptionStatus = SubscriptionStatusNames.Active, LimitsJson = "{}",
                CreatedAtUtc = start, UpdatedAtUtc = start
            });
            db.TenantSubscriptions.Add(new TenantSubscriptionEntity
            {
                TenantSubscriptionId = Guid.NewGuid(), OrganizationId = orgId, PlanCode = "starter",
                Status = SubscriptionStatusNames.Active, CurrentPeriodStartUtc = start, CurrentPeriodEndUtc = start.AddMonths(1),
                NextInvoiceUtc = start.AddMonths(1), AmountMinorUnits = 290000, CurrencyCode = "RUB",
                BillingInterval = BillingIntervalNames.Monthly, CreatedAtUtc = start, UpdatedAtUtc = start
            });
            await db.SaveChangesAsync();
        }

        // Fixed time well past the first NextInvoiceUtc so the first tick issues an invoice.
        var time = new FixedTimeProvider(start.AddMonths(2));
        var hosted = new InvoiceGenerationHostedService(
            provider, time, Options.Create(new BillingOptions { GenerationInterval = TimeSpan.FromHours(1) }),
            NullLogger<InvoiceGenerationHostedService>.Instance);

        using var cts = new CancellationTokenSource();
        await hosted.StartAsync(cts.Token);

        // Poll until the first tick has issued the invoice (the Task.Delay holds the loop afterward).
        InvoiceEntity? invoice = null;
        for (var attempt = 0; attempt < 50 && invoice is null; attempt++)
        {
            await Task.Delay(20, CancellationToken.None);
            await using var scope = provider.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            invoice = await db.Invoices.FirstOrDefaultAsync(CancellationToken.None);
        }

        await cts.CancelAsync();
        await hosted.StopAsync(CancellationToken.None);

        Assert.NotNull(invoice);
        Assert.Equal(290000, invoice!.AmountMinorUnits);
    }
}
```

- [ ] **Step 3: Run to verify it fails, then (after creating the file in Step 1) passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter InvoiceGenerationHostedServiceTests`
Expected: PASS (1 test). (If it was run before Step 1's file existed it would fail to compile.)

- [ ] **Step 4: Register the hosted service in DI**

In `Program.cs`, after the `IInvoiceService` registration:
```csharp
builder.Services.AddHostedService<InvoiceGenerationHostedService>();
```

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Platform/Billing/InvoiceGenerationHostedService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/InvoiceGenerationHostedServiceTests.cs
git commit -m "feat(platform): hourly invoice generation background job"
```

---

## Task 17: Invoice endpoints (list / generate / mark-paid / void) with idempotency

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (4 endpoints)
- Test: `tests/AFK4.Platform.Api.Tests/Billing/PlatformInvoiceEndpointTests.cs`

> The three mutating endpoints (generate / mark-paid / void) are financial actions, so they honor an optional `Idempotency-Key` header using the existing `IPlatformIdempotencyStore` + `IdempotencyKeyHelper`, mirroring the create-tenant endpoint (Program.cs lines 808–832, 884–891). The GET list endpoint does not.

- [ ] **Step 1: Write the failing test**

`PlatformInvoiceEndpointTests.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Tests.Platform;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Tenants;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class PlatformInvoiceEndpointTests
{
    private static async Task<Guid> CreateTenantAsync(HttpClient client, string slug = "inv-club")
    {
        var request = new CreateTenantRequest(
            OrganizationSlug: slug,
            OrganizationName: "Invoice Club",
            BranchSlug: "main",
            BranchName: "Main",
            BranchCity: "Dushanbe",
            PlanCode: TenantPlanCodeNames.Starter,
            SubscriptionStatus: SubscriptionStatusNames.Active,
            Limits: new TenantLimitsDto(1, 30, 40, 10),
            OwnerUserName: $"owner@{slug}.test",
            OwnerDisplayName: "Owner",
            OwnerInviteLifetime: TimeSpan.FromDays(7));
        var response = await client.PostAsJsonAsync("/api/platform/tenants", request);
        var body = await response.Content.ReadFromJsonAsync<CreateTenantResponse>();
        return body!.Tenant.OrganizationId;
    }

    [Fact]
    public async Task GenerateThenListThenMarkPaid_Flow()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(client);

        var generate = await client.PostAsync($"/api/platform/tenants/{orgId}/invoices/generate", content: null);
        var invoice = await generate.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(HttpStatusCode.OK, generate.StatusCode);

        var list = await client.GetAsync($"/api/platform/tenants/{orgId}/invoices");
        var invoices = await list.Content.ReadFromJsonAsync<List<InvoiceDto>>();
        Assert.Single(invoices!);

        var markPaid = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice!.InvoiceId}/mark-paid",
            new MarkInvoicePaidRequest("wire-123"));
        var paid = await markPaid.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatusNames.Paid, paid!.Status);
    }

    [Fact]
    public async Task VoidInvoice_RequiresReason()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var orgId = await CreateTenantAsync(client, "void-club");
        var generate = await client.PostAsync($"/api/platform/tenants/{orgId}/invoices/generate", content: null);
        var invoice = await generate.Content.ReadFromJsonAsync<InvoiceDto>();

        var noReason = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice!.InvoiceId}/void", new VoidInvoiceRequest(""));
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);

        var voided = await client.PostAsJsonAsync(
            $"/api/platform/invoices/{invoice.InvoiceId}/void", new VoidInvoiceRequest("test"));
        var body = await voided.Content.ReadFromJsonAsync<InvoiceDto>();
        Assert.Equal(InvoiceStatusNames.Void, body!.Status);
    }

    [Fact]
    public async Task GetInvoices_WithoutAuth_ReturnsUnauthorized()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/platform/tenants/{Guid.NewGuid()}/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformInvoiceEndpointTests`
Expected: FAIL — endpoints not mapped.

- [ ] **Step 3: Map the four endpoints** (in `Program.cs`, after the subscription endpoints)

```csharp
app.MapGet("/api/platform/tenants/{organizationId:guid}/invoices", async (
    Guid organizationId,
    string? status,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ViewBilling);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await invoiceService.ListForTenantAsync(organizationId, status, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});

app.MapPost("/api/platform/tenants/{organizationId:guid}/invoices/generate", async (
    Guid organizationId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { organizationId });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var prior = await idempotencyStore.TryReadAsync("platform.invoices.generate", idempotencyKey, requestHash, cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.GenerateAsync(organizationId, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter, organizationId, authorization.PlatformAdminContext!.PlatformAdminUserId,
        AuditActionNames.GenerateInvoice, "Invoice", result.Value!.InvoiceId.ToString("D"),
        AuditOutcome.Succeeded, new { result.Value.Number, result.Value.AmountMinorUnits }, cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = System.Text.Json.JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync("platform.invoices.generate", idempotencyKey, requestHash,
            authorization.PlatformAdminContext.PlatformAdminUserId, StatusCodes.Status200OK, responseBody, TimeSpan.FromHours(24), cancellationToken);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/platform/invoices/{invoiceId:guid}/mark-paid", async (
    Guid invoiceId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    MarkInvoicePaidRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var prior = await idempotencyStore.TryReadAsync("platform.invoices.mark_paid", idempotencyKey, requestHash, cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.MarkPaidAsync(invoiceId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter, result.Value!.OrganizationId, authorization.PlatformAdminContext!.PlatformAdminUserId,
        AuditActionNames.MarkInvoicePaid, "Invoice", invoiceId.ToString("D"),
        AuditOutcome.Succeeded, new { result.Value.Number, request.Reference }, cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = System.Text.Json.JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync("platform.invoices.mark_paid", idempotencyKey, requestHash,
            authorization.PlatformAdminContext.PlatformAdminUserId, StatusCodes.Status200OK, responseBody, TimeSpan.FromHours(24), cancellationToken);
    }

    return Results.Ok(result.Value);
});

app.MapPost("/api/platform/invoices/{invoiceId:guid}/void", async (
    Guid invoiceId,
    HttpContext httpContext,
    PlatformAdminAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    IPlatformIdempotencyStore idempotencyStore,
    IAuditRecordWriter auditRecordWriter,
    VoidInvoiceRequest request,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequirePermission(PlatformAdminPermissionNames.ManageInvoices);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var idempotencyKey = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    var requestHash = IdempotencyKeyHelper.HashRequest(new { invoiceId, request });
    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var prior = await idempotencyStore.TryReadAsync("platform.invoices.void", idempotencyKey, requestHash, cancellationToken);
        if (prior.RequestHashMismatch)
            return Results.Json(new { Error = "Idempotency-Key was reused with a different request body." }, statusCode: StatusCodes.Status422UnprocessableEntity);
        if (prior.Stored is not null)
        {
            httpContext.Response.Headers["Idempotency-Replayed"] = "true";
            return Results.Content(prior.Stored.ResponseBody, "application/json", statusCode: prior.Stored.StatusCode);
        }
    }

    var result = await invoiceService.VoidAsync(invoiceId, request, cancellationToken);
    if (!result.Succeeded)
        return BillingResults.From(result);

    await WritePlatformAuditAsync(
        auditRecordWriter, result.Value!.OrganizationId, authorization.PlatformAdminContext!.PlatformAdminUserId,
        AuditActionNames.VoidInvoice, "Invoice", invoiceId.ToString("D"),
        AuditOutcome.Succeeded, new { result.Value.Number, request.Reason }, cancellationToken);

    if (!string.IsNullOrWhiteSpace(idempotencyKey))
    {
        var responseBody = System.Text.Json.JsonSerializer.Serialize(result.Value, IdempotencyKeyHelper.JsonOptions);
        await idempotencyStore.WriteAsync("platform.invoices.void", idempotencyKey, requestHash,
            authorization.PlatformAdminContext.PlatformAdminUserId, StatusCodes.Status200OK, responseBody, TimeSpan.FromHours(24), cancellationToken);
    }

    return Results.Ok(result.Value);
});
```

> **Note on `IdempotencyKeyHelper`:** confirm the helper's namespace/usings already imported by the create-tenant endpoint (`IdempotencyKeyHelper.HashRequest`, `IdempotencyKeyHelper.JsonOptions`) and reuse the same — no new helper is created.

- [ ] **Step 4: Run the tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformInvoiceEndpointTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/PlatformInvoiceEndpointTests.cs
git commit -m "feat(platform): invoice endpoints (list/generate/mark-paid/void)"
```

---

## Task 18: Final gate — full build + full test run

**Files:** none (verification only)

- [ ] **Step 1: Full solution build**

Run: `dotnet build AFK4.sln`
Expected: PASS, no warnings introduced by billing code.

- [ ] **Step 2: Full platform test suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj`
Expected: PASS — all new billing tests plus all pre-existing tests (no regressions, especially `PlatformTenantEndpointTests`).

- [ ] **Step 3: Confirm the migration is present and the model snapshot is consistent**

Run: `dotnet ef migrations list -p src/AFK4.Platform.Api -s src/AFK4.Platform.Api`
Expected: `AddSaasSubscriptionBilling` appears as the latest migration. (Optional, if a local Postgres is available: `dotnet ef database update`.)

- [ ] **Step 4: Final commit (if any uncommitted verification artifacts)**

```bash
git status
# If clean, nothing to do. Otherwise:
git add -A
git commit -m "chore(platform): billing backend verification pass"
```

---

## Self-review notes (spec coverage)

Checked against `docs/superpowers/specs/2026-05-31-platform-admin-control-plane-design.md` §3 (Plan 3 scope) and §7 defaults:

- **`SubscriptionPlanEntity` (catalog, seeded starter/growth/scale, copies limits on assign)** → Tasks 3, 9, 11 (limits copied into org on plan change). ✓
- **`TenantSubscriptionEntity` (one active per org, source of truth, org kept in sync)** → Tasks 4, 11 (unique `OrganizationId` index; org `PlanCode`/`SubscriptionStatus`/`LimitsJson` synced on every write). ✓
- **`InvoiceEntity` (sequential number, single amount, statuses)** → Tasks 5, 14, 15. ✓
- **`InvoiceGenerationHostedService` (hourly, advance period, overdue flip, idempotent)** → Tasks 14 (logic + idempotency), 16 (host). ✓
- **Proration (one-off adjustment invoice on mid-cycle change)** → Task 11. ✓
- **Endpoints**: `GET/POST/PATCH /plans` (Task 10), `GET/PATCH /tenants/{id}/subscription` (Task 13), `GET /tenants/{id}/invoices` + `POST .../invoices/generate` + `POST /invoices/{id}/mark-paid` + `POST /invoices/{id}/void` (Task 17). ✓
- **Migrate `/plan` semantics into subscription** → Task 11 (subscription is source of truth; legacy `/plan` left functional, removed later per design decision #1). ✓
- **Permissions in `PlatformAdminPermissionNames`** → Task 1. ✓
- **Idempotency (`IPlatformIdempotencyStore`) on financial actions; audit (`IAuditRecordWriter`)** → Task 17 (idempotency) + Tasks 10/13/17 (audit). ✓
- **Defaults: currency RUB, interval monthly, sequential numbering, subscription = source of truth** → Tasks 5/9/11 + design decisions. ✓
- **Out of scope respected**: no payment provider, no line items (single amount), no usage metering. ✓
- **Club-side endpoints (`/api/organizations/{id}/subscription|invoices`) and the `/club/billing` screen** are **Plan 7**, intentionally excluded here. The design spec §3 lists them under the club-side bullet; §5 places them in Plan 7. Not in Plan 3.
