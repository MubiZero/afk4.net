# Platform Club-Side Billing (SP3 Plan 7) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give a club **owner** a read-only `/club/billing` screen showing their organization's current subscription (plan, status, period, next-invoice, limits, amount) and invoice history, backed by two new org-scoped read endpoints, and flip the `clubNav` `billing` item from `soon: true` to a live screen.

**Architecture:** Full-stack, read-only. Backend adds one new staff permission `billing.subscription.view` (granted **owner-only**) and two org-scoped GET endpoints (`GET /api/organizations/{organizationId}/subscription`, `GET /api/organizations/{organizationId}/invoices`) that **reuse** the existing `ITenantSubscriptionService.GetAsync` / `IInvoiceService.ListForTenantAsync` services (built in Plan 3). The endpoints authorize with `StaffAuthorizationService.RequireOrganizationPermission` **and** enforce that the route `organizationId` equals the caller's own `StaffContext.OrganizationId` (IDOR prevention — see Security note). Frontend adds a `src/club/billing/` feature module (pure `billingModel.ts` builder + discriminated-union `useBilling` hook + presentational `BillingScreen`) mirroring `src/club/reports/`, two new `ClubApiClient` methods, the `clubBilling` route wired across `App.tsx`, and `club.billing.*` i18n keys.

**Tech Stack:** ASP.NET minimal APIs + EF Core (`PlatformDbContext`) + xUnit (`PlatformApiFactory`, `StaffAuthTestHelper`); React + TypeScript + Tailwind v4 + shadcn-style primitives (`@/components/ui/*`) + Vitest (`globals:false`).

---

## Conventions (read before starting)

- **Frontend build gate is `npm run build` (`tsc -b && vite build`), NOT `npm test`.** Vitest/esbuild skips type-checks. Run **both** `npm run build` and `npm test` at every frontend checkpoint. Frontend commands run from `src/AFK4.Platform.Web`.
- **Backend gate:** from repo root, `dotnet build` then `dotnet test` (in-memory EF via `PlatformApiFactory`). Re-run before final commit.
- **Money = integer minor units** in DTOs (`amountMinorUnits`); the UI formats with `formatCurrency(minorToMajor(x), code)` from `@/club/money` + `useI18n()`. `formatCurrency` expects **MAJOR** units — you MUST convert via `minorToMajor` (`÷100`). NEVER pass `amountMinorUnits` straight to `formatCurrency` (this caused a 100× bug in Plan 4). Canonical example: `src/club/reports/reportsModel.ts`.
- **Vitest imports are explicit** (`globals:false`): `import { describe, it, expect, vi } from 'vitest';`.
- **i18n parity is enforced** by `src/i18n/messages.test.ts`. Every key added to the `ru` block MUST also be added to `en`, or the suite fails.
- **`src/preview/DemoApp.tsx` is the user's UNTRACKED scratch.** Never `git add` it. This plan changes no shared shell prop contract, so it should be unaffected.
- **Existing types reused (do NOT redefine):** `TenantSubscription` and `Invoice` interfaces already exist in `src/api/types.ts` (added in Plan 4). The club endpoints return the same JSON shape as the admin ones (`TenantSubscriptionDto` / `InvoiceDto`).
- **Backend `BillingOperationResult<T>` + `BillingResults.From(result)`** are the existing result/error-mapping helpers (Plan 3). Reuse, don't reinvent. `GetAsync`/`ListForTenantAsync` return `BillingOperationResult<...>`.

## Scope note — limits (deviation from spec wording)

Spec §4.5 lists "**limits**" among the owner-view fields, but `TenantSubscriptionDto` (the shape returned by `GetAsync`) carries **no** limit fields, and there is **no** club-side plan-catalog endpoint. Showing limits would require either a third org-scoped read (`GET /api/organizations/{orgId}/plan` exposing the catalog limits) or surfacing `OrganizationEntity.LimitsJson`. To keep Plan 7 to its locked "read-only, reuse existing services" shape, **this plan omits the limits panel.** If limits are wanted, it's a small follow-up (one more endpoint + a `Card` section) tracked separately. The fields shown are: plan, status, amount, current period, next-invoice date, cancel-at-period-end, and invoice history.

## Security note (the one real design decision)

`RequireOrganizationPermission(permission)` (`src/AFK4.Platform.Api/Identity/StaffAuthorizationService.cs:41`) checks **only** the permission against the current staff context — it does **not** validate any route org id. A naive handler that forwards the route `organizationId` to the service would let a staff member of org A read org B's billing by changing the URL (IDOR). Therefore both handlers MUST, after the permission check, verify `organizationId == authorization.StaffContext!.OrganizationId` and return `403 Forbidden` on mismatch, then call the service with the **trusted** `authorization.StaffContext!.OrganizationId`. Task 3 includes an explicit cross-org test that locks this behavior.

---

## File Structure

**Backend:**
- Modify `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs` — add `ViewSubscription = "billing.subscription.view"`.
- Modify `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs` — grant `ViewSubscription` to the `Owner` role only.
- Modify `src/AFK4.Platform.Api/Program.cs` — add two org-scoped `MapGet` handlers.
- Create `tests/AFK4.Platform.Api.Tests/ClubBillingEndpointTests.cs` — endpoint + auth + IDOR coverage.

**Frontend — `src/AFK4.Platform.Web/`:**
- Modify `src/api/clubApi.ts` — add `getSubscription(orgId)` + `listInvoices(orgId)`.
- Create `src/club/billing/billingModel.ts` (+ `.test.ts`) — pure status maps + view builders.
- Create `src/club/billing/useBilling.ts` (+ `.test.tsx`) — discriminated-union hook (parallel load).
- Create `src/club/billing/BillingScreen.tsx` (+ `.test.tsx`) — presentational screen.
- Modify `src/club/nav.ts` — flip `billing` `soon: true` → `false`.
- Modify `src/club/nav.test.ts` — update the assertion that currently expects `billing` to be `soon` (if present).
- Modify `src/App.tsx` — wire `clubBilling` route across the six club-routing anchors.
- Modify `src/i18n/messages.ts` — add `club.billing.*` keys (ru + en).

---

## Phase A — Backend org-scoped read endpoints

### Task 1: Add the `billing.subscription.view` permission (owner-only)

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs`
- Modify: `src/AFK4.Platform.Api/Identity/PermissionCatalog.cs:10-62` (Owner role set)

- [ ] **Step 1: Add the constant**

In `StaffPermissionNames.cs`, directly after the existing `PayDebt` constant (line ~45), add:

```csharp
    public const string ViewSubscription = "billing.subscription.view";
```

- [ ] **Step 2: Grant it to the Owner role only**

In `PermissionCatalog.cs`, inside the `[StaffRoleNames.Owner] = new HashSet<string> { ... }` block, add a line next to the other billing permissions (after `StaffPermissionNames.PayDebt,` at line ~32):

```csharp
                StaffPermissionNames.ViewSubscription,
```

Do **not** add it to `BranchManager`, `AccountantAuditor`, or any other role — the club billing screen is owner-only per spec.

- [ ] **Step 3: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Shared.Contracts/Identity/StaffPermissionNames.cs src/AFK4.Platform.Api/Identity/PermissionCatalog.cs
git commit -m "feat(platform): owner-only billing.subscription.view staff permission"
```

---

### Task 2: Org-scoped subscription + invoices endpoints

**Files:**
- Modify: `src/AFK4.Platform.Api/Program.cs` (add two handlers immediately after the platform-admin billing block, i.e. after the `GET /api/platform/tenants/{organizationId:guid}/invoices` handler that closes at line ~1702)

- [ ] **Step 1: Add the `GET /api/organizations/{organizationId}/subscription` handler**

Insert after line ~1702 (mirrors the admin handler at `Program.cs:1602`, swapping platform-admin auth for org-scoped auth + the IDOR guard; no audit writes, matching org-scoped read endpoints like `GET /api/staff/me/owner-code`):

```csharp
// --- Club-side (owner) read-only billing (SP3 Plan 7) ---
app.MapGet("/api/organizations/{organizationId:guid}/subscription", async (
    Guid organizationId,
    StaffAuthorizationService authorizationService,
    ITenantSubscriptionService subscriptionService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewSubscription);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (organizationId != authorization.StaffContext!.OrganizationId)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await subscriptionService.GetAsync(authorization.StaffContext!.OrganizationId, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});
```

- [ ] **Step 2: Add the `GET /api/organizations/{organizationId}/invoices` handler**

Directly below the Step 1 handler, insert (mirrors the admin handler at `Program.cs:1674`; passes `status: null` — owner view is the full history):

```csharp
app.MapGet("/api/organizations/{organizationId:guid}/invoices", async (
    Guid organizationId,
    StaffAuthorizationService authorizationService,
    IInvoiceService invoiceService,
    CancellationToken cancellationToken) =>
{
    var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ViewSubscription);
    if (!authorization.IsAuthenticated)
        return Results.Unauthorized();
    if (!authorization.IsAllowed)
        return Results.StatusCode(StatusCodes.Status403Forbidden);
    if (organizationId != authorization.StaffContext!.OrganizationId)
        return Results.StatusCode(StatusCodes.Status403Forbidden);

    var result = await invoiceService.ListForTenantAsync(authorization.StaffContext!.OrganizationId, status: null, cancellationToken);
    return result.Succeeded ? Results.Ok(result.Value) : BillingResults.From(result);
});
```

> NOTE: `StaffAuthorizationService`, `ITenantSubscriptionService`, `IInvoiceService`, `BillingResults`, and `StaffPermissionNames` are already `using`-imported in `Program.cs` (used by existing handlers). If `dotnet build` flags a missing `using`, add it — but it should not.

- [ ] **Step 3: Build**

Run: `dotnet build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj`
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Program.cs
git commit -m "feat(platform): org-scoped GET subscription + invoices endpoints"
```

---

### Task 3: Endpoint tests (auth, success, IDOR, permission)

**Files:**
- Create: `tests/AFK4.Platform.Api.Tests/ClubBillingEndpointTests.cs`

The test harness `StaffAuthTestHelper.AuthorizeAsAsync(factory, client, roleName)` seeds a staff user + an `OrganizationEntity` with `TestIds.OrganizationId` and signs the client in. We additionally seed a `TenantSubscriptionEntity` (and one `InvoiceEntity`) for that org so the read endpoints return data.

- [ ] **Step 1: Write the test file**

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests;

public sealed class ClubBillingEndpointTests
{
    [Fact]
    public async Task GET_subscription_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscription_as_owner_returns_own_subscription()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);

        var subscription = await client.GetFromJsonAsync<TenantSubscriptionDto>(
            $"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.NotNull(subscription);
        Assert.Equal(TestIds.OrganizationId, subscription!.OrganizationId);
    }

    [Fact]
    public async Task GET_subscription_rejects_other_org_with_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);

        var response = await client.GetAsync($"/api/organizations/{Guid.NewGuid():D}/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_subscription_without_permission_returns_403()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Technician);

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/subscription");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GET_invoices_as_owner_returns_rows()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthorizeAsAsync(factory, client, StaffRoleNames.Owner);
        await SeedSubscriptionAsync(factory);
        await SeedInvoiceAsync(factory);

        var invoices = await client.GetFromJsonAsync<List<InvoiceDto>>(
            $"/api/organizations/{TestIds.OrganizationId:D}/invoices");

        Assert.NotNull(invoices);
        Assert.Contains(invoices!, i => i.OrganizationId == TestIds.OrganizationId);
    }

    [Fact]
    public async Task GET_invoices_requires_auth()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync($"/api/organizations/{TestIds.OrganizationId:D}/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task SeedSubscriptionAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.TenantSubscriptions.Add(new TenantSubscriptionEntity
        {
            TenantSubscriptionId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            PlanCode = "starter",
            Status = SubscriptionStatusNames.Active,
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
    }

    private static async Task SeedInvoiceAsync(PlatformApiFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Invoices.Add(new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            Number = 5001,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = now.AddMonths(-1),
            PeriodEndUtc = now,
            IssuedAtUtc = now,
            DueAtUtc = now.AddDays(7),
            AmountMinorUnits = 290000,
            CurrencyCode = "RUB",
            Status = InvoiceStatusNames.Issued,
            Description = "club billing test",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await db.SaveChangesAsync();
    }
}
```

> NOTE: If the compiler flags that `TenantSubscriptionEntity`/`InvoiceEntity` require additional non-nullable columns, or that a status-name constant lives in a different namespace, fix the seed using the same defaults already used by `tests/AFK4.Platform.Api.Tests/Platform/BillingListEndpointTests.cs` (`SeedOrgWithSubscriptionAsync`/`SeedInvoice`) — copy those field-for-field. Do not change entities. If `StaffAuthTestHelper` already seeds a subscription for `TestIds.OrganizationId`, drop `SeedSubscriptionAsync` and rely on the seeded one (a duplicate-key error from `SaveChangesAsync` is the signal).

- [ ] **Step 2: Run the new tests**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~ClubBillingEndpointTests`
Expected: 6 passing.

- [ ] **Step 3: Full backend gate + commit**

Run: `dotnet build` then `dotnet test`
Expected: Build 0 errors; all tests pass.

```bash
git add tests/AFK4.Platform.Api.Tests/ClubBillingEndpointTests.cs
git commit -m "test(platform): club-side billing endpoint auth + IDOR coverage"
```

---

## Phase B — Frontend API client

### Task 4: `ClubApiClient` billing methods

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/clubApi.ts`

- [ ] **Step 1: Import the DTO types**

Extend the existing `import type { ... } from './types';` block in `clubApi.ts` to also import `Invoice` and `TenantSubscription` (merge alphabetically into the existing group; do not duplicate the import line).

- [ ] **Step 2: Add the two methods**

Add to the `ClubApiClient` class, next to the other GET methods (e.g. after `getBranchProfile`, ~line 104), following the existing param-passing convention (`organizationId` supplied by the caller from `session.organizationId`):

```ts
  public getSubscription(organizationId: string): Promise<TenantSubscription> {
    return this.send<TenantSubscription>('GET', `/api/organizations/${encodeURIComponent(organizationId)}/subscription`);
  }

  public listInvoices(organizationId: string): Promise<Invoice[]> {
    return this.send<Invoice[]>('GET', `/api/organizations/${encodeURIComponent(organizationId)}/invoices`);
  }
```

- [ ] **Step 3: Type-check + commit**

Run (from `src/AFK4.Platform.Web`): `npm run build`
Expected: success.

```bash
git add src/AFK4.Platform.Web/src/api/clubApi.ts
git commit -m "feat(platform-web): club billing API client methods"
```

---

## Phase C — Club billing module

### Task 5: `billingModel.ts` — pure status maps + view builders

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/billing/billingModel.ts`
- Test: `src/AFK4.Platform.Web/src/club/billing/billingModel.test.ts`

The model is pure (no React, no client). It maps subscription/invoice statuses to i18n label keys + badge variants and converts a `TenantSubscription` into summary rows and `Invoice[]` into table rows. Currency is formatted by the caller via `formatCurrency`; the model exposes the raw pieces plus a `money()` helper that applies `minorToMajor`.

- [ ] **Step 1: Write the failing test**

```ts
import { describe, it, expect } from 'vitest';
import {
  subscriptionStatusLabelKey,
  subscriptionStatusVariant,
  invoiceStatusLabelKey,
  invoiceStatusVariant,
  buildInvoiceRows
} from './billingModel';
import type { Invoice } from '../../api/types';

describe('billingModel', () => {
  it('maps known subscription statuses to label keys and variants', () => {
    expect(subscriptionStatusLabelKey('active')).toBe('club.billing.subStatus.active');
    expect(subscriptionStatusVariant('past_due')).toBe('warning');
    expect(subscriptionStatusVariant('cancelled')).toBe('muted');
  });

  it('falls back to the raw status for unknown subscription statuses', () => {
    expect(subscriptionStatusLabelKey('weird')).toBeNull();
    expect(subscriptionStatusVariant('weird')).toBe('muted');
  });

  it('maps invoice statuses', () => {
    expect(invoiceStatusLabelKey('paid')).toBe('club.billing.invStatus.paid');
    expect(invoiceStatusVariant('overdue')).toBe('danger');
  });

  it('builds invoice rows newest amount intact (minor units preserved)', () => {
    const invoices: Invoice[] = [
      {
        invoiceId: 'i1', organizationId: 'o1', number: 12, kind: 'subscription',
        periodStartUtc: '2026-04-01T00:00:00Z', periodEndUtc: '2026-05-01T00:00:00Z',
        issuedAtUtc: '2026-05-01T00:00:00Z', dueAtUtc: '2026-05-08T00:00:00Z',
        amountMinorUnits: 290000, currencyCode: 'RUB', status: 'issued',
        paidAtUtc: null, voidedAtUtc: null, voidReason: null, description: 'x'
      }
    ];
    const rows = buildInvoiceRows(invoices);
    expect(rows).toHaveLength(1);
    expect(rows[0].number).toBe(12);
    expect(rows[0].amountMinorUnits).toBe(290000);
    expect(rows[0].currencyCode).toBe('RUB');
  });
});
```

- [ ] **Step 2: Run it to confirm it fails**

Run (from `src/AFK4.Platform.Web`): `npm test -- billingModel`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `billingModel.ts`**

```ts
import type { MessageKey } from '../../i18n/messages';
import type { Invoice } from '../../api/types';

export type BadgeVariant = 'success' | 'warning' | 'danger' | 'muted';

const SUB_STATUS_LABEL: Record<string, MessageKey> = {
  trial: 'club.billing.subStatus.trial',
  active: 'club.billing.subStatus.active',
  past_due: 'club.billing.subStatus.pastDue',
  cancelled: 'club.billing.subStatus.cancelled'
};

const SUB_STATUS_VARIANT: Record<string, BadgeVariant> = {
  trial: 'warning',
  active: 'success',
  past_due: 'warning',
  cancelled: 'muted'
};

const INV_STATUS_LABEL: Record<string, MessageKey> = {
  issued: 'club.billing.invStatus.issued',
  paid: 'club.billing.invStatus.paid',
  void: 'club.billing.invStatus.void',
  overdue: 'club.billing.invStatus.overdue'
};

const INV_STATUS_VARIANT: Record<string, BadgeVariant> = {
  issued: 'warning',
  paid: 'success',
  void: 'muted',
  overdue: 'danger'
};

export function subscriptionStatusLabelKey(status: string): MessageKey | null {
  return SUB_STATUS_LABEL[status] ?? null;
}

export function subscriptionStatusVariant(status: string): BadgeVariant {
  return SUB_STATUS_VARIANT[status] ?? 'muted';
}

export function invoiceStatusLabelKey(status: string): MessageKey | null {
  return INV_STATUS_LABEL[status] ?? null;
}

export function invoiceStatusVariant(status: string): BadgeVariant {
  return INV_STATUS_VARIANT[status] ?? 'muted';
}

export interface InvoiceRow {
  invoiceId: string;
  number: number;
  kind: string;
  issuedAtUtc: string;
  dueAtUtc: string;
  amountMinorUnits: number;
  currencyCode: string;
  status: string;
}

export function buildInvoiceRows(invoices: Invoice[]): InvoiceRow[] {
  return invoices.map(i => ({
    invoiceId: i.invoiceId,
    number: i.number,
    kind: i.kind,
    issuedAtUtc: i.issuedAtUtc,
    dueAtUtc: i.dueAtUtc,
    amountMinorUnits: i.amountMinorUnits,
    currencyCode: i.currencyCode,
    status: i.status
  }));
}
```

> NOTE: `MessageKey` is `keyof (typeof messages)['ru']`. The label keys referenced here (`club.billing.subStatus.*`, `club.billing.invStatus.*`) are added in Task 8; until then `tsc -b` will flag them. Implement Task 8's i18n keys before the first full `npm run build`, or expect those specific key errors in the interim. (Tasks may be executed in order; if so, this resolves at Task 8.)

- [ ] **Step 4: Run the test**

Run: `npm test -- billingModel`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/billing/billingModel.ts src/AFK4.Platform.Web/src/club/billing/billingModel.test.ts
git commit -m "feat(platform-web): club billing model (status maps + invoice rows)"
```

---

### Task 6: `useBilling.ts` — discriminated-union hook (parallel load)

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/billing/useBilling.ts`
- Test: `src/AFK4.Platform.Web/src/club/billing/useBilling.test.tsx`

Loads subscription + invoices together via `Promise.all`, mirroring `src/club/overview/useOverview.ts`. Discriminated union `loading | error | ready`; all states carry `retry`; client held in a `useRef`; deps `[organizationId, tick]`.

- [ ] **Step 1: Write the failing test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { renderHook, waitFor, act } from '@testing-library/react';
import { useBilling } from './useBilling';
import type { ClubApiClient } from '../../api/clubApi';
import type { Invoice, TenantSubscription } from '../../api/types';

function makeSubscription(): TenantSubscription {
  return {
    tenantSubscriptionId: 's1', organizationId: 'o1', planCode: 'starter', status: 'active',
    currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
    nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
    billingInterval: 'monthly', cancelAtPeriodEnd: false,
    createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
  };
}

function makeClient(over: Partial<ClubApiClient>): ClubApiClient {
  return {
    getSubscription: vi.fn().mockResolvedValue(makeSubscription()),
    listInvoices: vi.fn().mockResolvedValue([] as Invoice[]),
    ...over
  } as unknown as ClubApiClient;
}

describe('useBilling', () => {
  it('reaches ready with subscription + invoices', async () => {
    const client = makeClient({});
    const { result } = renderHook(() => useBilling(client, 'o1'));
    expect(result.current.status).toBe('loading');
    await waitFor(() => expect(result.current.status).toBe('ready'));
    if (result.current.status === 'ready') {
      expect(result.current.subscription.planCode).toBe('starter');
      expect(result.current.invoices).toEqual([]);
    }
  });

  it('reaches error and can retry', async () => {
    const getSubscription = vi.fn()
      .mockRejectedValueOnce(new Error('boom'))
      .mockResolvedValue(makeSubscription());
    const client = makeClient({ getSubscription });
    const { result } = renderHook(() => useBilling(client, 'o1'));
    await waitFor(() => expect(result.current.status).toBe('error'));
    act(() => result.current.retry());
    await waitFor(() => expect(result.current.status).toBe('ready'));
  });
});
```

- [ ] **Step 2: Run it to confirm it fails**

Run: `npm test -- useBilling`
Expected: FAIL (module not found).

- [ ] **Step 3: Implement `useBilling.ts`**

```ts
import { useCallback, useEffect, useRef, useState } from 'react';
import type { ClubApiClient } from '../../api/clubApi';
import type { Invoice, TenantSubscription } from '../../api/types';

export type BillingState =
  | { status: 'loading' }
  | { status: 'error'; retry: () => void }
  | { status: 'ready'; subscription: TenantSubscription; invoices: Invoice[]; retry: () => void };

export function useBilling(client: ClubApiClient, organizationId: string): BillingState {
  const [tick, setTick] = useState(0);
  const [phase, setPhase] = useState<'loading' | 'error' | 'ready'>('loading');
  const [subscription, setSubscription] = useState<TenantSubscription | null>(null);
  const [invoices, setInvoices] = useState<Invoice[]>([]);
  const clientRef = useRef(client);
  clientRef.current = client;

  const retry = useCallback(() => setTick(t => t + 1), []);

  useEffect(() => {
    let cancelled = false;
    setPhase('loading');
    Promise.all([
      clientRef.current.getSubscription(organizationId),
      clientRef.current.listInvoices(organizationId)
    ])
      .then(([sub, inv]) => {
        if (cancelled) return;
        setSubscription(sub);
        setInvoices(inv);
        setPhase('ready');
      })
      .catch(() => {
        if (!cancelled) setPhase('error');
      });
    return () => { cancelled = true; };
  }, [organizationId, tick]);

  if (phase === 'error') return { status: 'error', retry };
  if (phase === 'loading' || subscription === null) return { status: 'loading' };
  return { status: 'ready', subscription, invoices, retry };
}
```

> NOTE: Confirm against `src/club/overview/useOverview.ts` that the discriminated-union shape and `useRef` client pattern match exactly; align naming if the exemplar differs.

- [ ] **Step 4: Run the test**

Run: `npm test -- useBilling`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/billing/useBilling.ts src/AFK4.Platform.Web/src/club/billing/useBilling.test.tsx
git commit -m "feat(platform-web): useBilling hook (parallel subscription + invoices load)"
```

---

### Task 7: `BillingScreen.tsx` — presentational owner screen

**Files:**
- Create: `src/AFK4.Platform.Web/src/club/billing/BillingScreen.tsx`
- Test: `src/AFK4.Platform.Web/src/club/billing/BillingScreen.test.tsx`

Renders the three hook states using shared primitives. Ready state = a summary `Card` (plan, status badge, current period, next-invoice date, amount, cancel-at-period-end flag) + an invoices `Table` (number, issued, due, amount, status badge) with an `EmptyState` when there are none. Read-only — no actions.

- [ ] **Step 1: Inspect a reference screen**

Read `src/club/reports/ReportsScreen.tsx` (or `ReportTab.tsx`) and `src/club/overview/OverviewScreen.tsx` to copy the exact imports/usage of `Card`, `Table`, `Badge`, `LoadingCards`, `ErrorState`, `EmptyState`, and `useI18n()`. Use the same `formatCurrency`/`formatDate` helpers. Match prop names exactly.

- [ ] **Step 2: Write the test**

```tsx
import { describe, it, expect, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { BillingScreen } from './BillingScreen';
import type { ClubApiClient } from '../../api/clubApi';
import type { Invoice, TenantSubscription } from '../../api/types';

function sub(): TenantSubscription {
  return {
    tenantSubscriptionId: 's1', organizationId: 'o1', planCode: 'starter', status: 'active',
    currentPeriodStartUtc: '2026-05-01T00:00:00Z', currentPeriodEndUtc: '2026-06-01T00:00:00Z',
    nextInvoiceUtc: '2026-06-01T00:00:00Z', amountMinorUnits: 290000, currencyCode: 'RUB',
    billingInterval: 'monthly', cancelAtPeriodEnd: false,
    createdAtUtc: '2026-05-01T00:00:00Z', updatedAtUtc: '2026-05-01T00:00:00Z'
  };
}

function client(invoices: Invoice[]): ClubApiClient {
  return {
    getSubscription: vi.fn().mockResolvedValue(sub()),
    listInvoices: vi.fn().mockResolvedValue(invoices)
  } as unknown as ClubApiClient;
}

describe('BillingScreen', () => {
  it('renders the current plan once loaded', async () => {
    render(<BillingScreen client={client([])} organizationId="o1" />);
    await waitFor(() => expect(screen.getByText('starter')).toBeInTheDocument());
  });

  it('shows the empty state when there are no invoices', async () => {
    render(<BillingScreen client={client([])} organizationId="o1" />);
    await waitFor(() => expect(screen.getByText(/Нет инвойсов|No invoices/)).toBeInTheDocument());
  });
});
```

> NOTE: The empty-state assertion text must match the `club.billing.invoices.empty` value added in Task 8 (`Нет инвойсов.` ru). If `BillingScreen` is wrapped in an i18n provider in other club screen tests, copy that test setup verbatim from `src/club/reports/*.test.tsx`. If `useI18n()` works without a provider (default ru), no wrapper is needed — match the exemplar.

- [ ] **Step 3: Implement `BillingScreen.tsx`**

Mirror the reference screen structure. Skeleton (adjust primitive imports/props to match the exemplar found in Step 1):

```tsx
import { useI18n } from '../../i18n/useI18n';
import { minorToMajor } from '../money';
import type { ClubApiClient } from '../../api/clubApi';
import { useBilling } from './useBilling';
import {
  subscriptionStatusLabelKey,
  subscriptionStatusVariant,
  invoiceStatusLabelKey,
  invoiceStatusVariant,
  buildInvoiceRows
} from './billingModel';
// import shared primitives (Card, Table, Badge, LoadingCards, ErrorState, EmptyState) — names per Step 1

export function BillingScreen({ client, organizationId }: { client: ClubApiClient; organizationId: string }) {
  const { t, formatCurrency, formatDate } = useI18n();
  const state = useBilling(client, organizationId);

  if (state.status === 'loading') return <LoadingCards />;
  if (state.status === 'error') return <ErrorState onRetry={state.retry} />;

  const { subscription, invoices } = state;
  const subLabel = subscriptionStatusLabelKey(subscription.status);
  const rows = buildInvoiceRows(invoices);

  return (
    <div className="space-y-6">
      <Card>
        {/* plan */}
        <Row label={t('club.billing.subscription.plan')} value={subscription.planCode} />
        {/* status badge */}
        <Badge variant={subscriptionStatusVariant(subscription.status)}>
          {subLabel ? t(subLabel) : subscription.status}
        </Badge>
        <Row label={t('club.billing.subscription.amount')}
             value={formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)} />
        <Row label={t('club.billing.subscription.period')}
             value={`${formatDate(subscription.currentPeriodStartUtc)} — ${formatDate(subscription.currentPeriodEndUtc)}`} />
        <Row label={t('club.billing.subscription.nextInvoice')}
             value={subscription.nextInvoiceUtc ? formatDate(subscription.nextInvoiceUtc) : '—'} />
      </Card>

      <Card>
        <h2>{t('club.billing.invoices.title')}</h2>
        {rows.length === 0 ? (
          <EmptyState message={t('club.billing.invoices.empty')} />
        ) : (
          <Table>
            {rows.map(r => {
              const invLabel = invoiceStatusLabelKey(r.status);
              return (
                <tr key={r.invoiceId}>
                  <td>{r.number}</td>
                  <td>{formatDate(r.issuedAtUtc)}</td>
                  <td>{formatDate(r.dueAtUtc)}</td>
                  <td>{formatCurrency(minorToMajor(r.amountMinorUnits), r.currencyCode)}</td>
                  <td><Badge variant={invoiceStatusVariant(r.status)}>{invLabel ? t(invLabel) : r.status}</Badge></td>
                </tr>
              );
            })}
          </Table>
        )}
      </Card>
    </div>
  );
}
```

> NOTE: `Row` is illustrative — use whatever label/value or definition-list primitive the exemplar club screens use (e.g. a `<dl>` or a shared `Field`). The `formatCurrency(minorToMajor(...), code)` boundary conversion is mandatory. Replace `LoadingCards`/`ErrorState`/`EmptyState`/`Card`/`Table`/`Badge` with the exact imports from `@/components/ui/*` / `@/components/ui/states` confirmed in Step 1.

- [ ] **Step 4: Run the test + build**

Run: `npm test -- BillingScreen` then `npm run build`
Expected: tests PASS; `tsc -b` + `vite build` succeed (after Task 8's keys exist).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/club/billing/BillingScreen.tsx src/AFK4.Platform.Web/src/club/billing/BillingScreen.test.tsx
git commit -m "feat(platform-web): read-only club BillingScreen"
```

---

## Phase D — i18n, routing, nav flip

### Task 8: `club.billing.*` i18n keys (ru + en)

**Files:**
- Modify: `src/AFK4.Platform.Web/src/i18n/messages.ts`

- [ ] **Step 1: Add the keys to BOTH the `ru` and `en` blocks**

Add this block inside the `ru` object (place near other `club.*` keys):

```ts
  'club.billing.title': 'Биллинг',
  'club.billing.subscription.title': 'Текущая подписка',
  'club.billing.subscription.plan': 'Тариф',
  'club.billing.subscription.status': 'Статус',
  'club.billing.subscription.amount': 'Сумма',
  'club.billing.subscription.period': 'Текущий период',
  'club.billing.subscription.nextInvoice': 'Следующий инвойс',
  'club.billing.subStatus.trial': 'Пробный',
  'club.billing.subStatus.active': 'Активна',
  'club.billing.subStatus.pastDue': 'Просрочена',
  'club.billing.subStatus.cancelled': 'Отменена',
  'club.billing.invoices.title': 'Инвойсы',
  'club.billing.invoices.empty': 'Нет инвойсов.',
  'club.billing.invStatus.issued': 'Выставлен',
  'club.billing.invStatus.paid': 'Оплачен',
  'club.billing.invStatus.void': 'Аннулирован',
  'club.billing.invStatus.overdue': 'Просрочен',
```

Add the parallel block inside the `en` object:

```ts
  'club.billing.title': 'Billing',
  'club.billing.subscription.title': 'Current subscription',
  'club.billing.subscription.plan': 'Plan',
  'club.billing.subscription.status': 'Status',
  'club.billing.subscription.amount': 'Amount',
  'club.billing.subscription.period': 'Current period',
  'club.billing.subscription.nextInvoice': 'Next invoice',
  'club.billing.subStatus.trial': 'Trial',
  'club.billing.subStatus.active': 'Active',
  'club.billing.subStatus.pastDue': 'Past due',
  'club.billing.subStatus.cancelled': 'Cancelled',
  'club.billing.invoices.title': 'Invoices',
  'club.billing.invoices.empty': 'No invoices.',
  'club.billing.invStatus.issued': 'Issued',
  'club.billing.invStatus.paid': 'Paid',
  'club.billing.invStatus.void': 'Void',
  'club.billing.invStatus.overdue': 'Overdue',
```

- [ ] **Step 2: Verify `nav.billing` parity**

Confirm `nav.billing` exists in BOTH `ru` and `en` (it is referenced by the nav item). If it exists only in `ru`, add `'nav.billing': 'Billing',` to `en`. (If parity already holds, `messages.test.ts` is green — leave it.)

- [ ] **Step 3: Run the parity test**

Run: `npm test -- messages`
Expected: PASS (ru/en parity).

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Web/src/i18n/messages.ts
git commit -m "feat(platform-web): club.billing i18n keys (ru + en)"
```

---

### Task 9: Wire the `clubBilling` route + flip nav `soon`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/App.tsx` (six club-routing anchors)
- Modify: `src/AFK4.Platform.Web/src/club/nav.ts`
- Modify: `src/AFK4.Platform.Web/src/club/nav.test.ts` (if it asserts billing `soon`)

This mirrors exactly how Plan 6 wired `adminProfile` on the admin side. Follow the existing `clubProfile`/`clubBranches` route end-to-end and add a parallel `clubBilling` case at each anchor.

- [ ] **Step 1: Add the route kind to the `ClubRoute` union**

In `App.tsx`, add `| { kind: 'clubBilling' }` to the `ClubRoute` union (next to `clubProfile`/`clubBranches`).

- [ ] **Step 2: Add it to the `isClubRoute` discriminator**

Add `|| route.kind === 'clubBilling'` to the `isClubRoute` predicate.

- [ ] **Step 3: Map the path → route**

In the route-resolution function (the one with `if (path === '/club/reports') return { route: { kind: 'clubReports' } };`), add:

```ts
  if (path === '/club/billing') {
    return { route: { kind: 'clubBilling' } };
  }
```

- [ ] **Step 4: Add the screen title**

In `CLUB_SCREEN_TITLE`, add: `clubBilling: 'Биллинг',`.

- [ ] **Step 5: Add `pathForRoute` case**

In `pathForRoute`, add: `case 'clubBilling': return '/club/billing';`.

- [ ] **Step 6: Render the screen in `ClubArea`**

In the `ClubArea` render chain, add a branch for `clubBilling` that renders the new screen, passing the client + the session's org id:

```tsx
  route.kind === 'clubBilling' ? (
    <BillingScreen client={clubClient} organizationId={session.organizationId} />
  ) :
```

Add the import: `import { BillingScreen } from './club/billing/BillingScreen';` (match the existing relative-import style used for other club screens in `App.tsx`). Confirm the session variable name and that `session.organizationId` is in scope (it is — `StaffSession.organizationId`).

- [ ] **Step 7: Flip the nav `soon` flag**

In `src/club/nav.ts`, change the `billing` item from `soon: true` to `soon: false` (keep `ownerOnly: true`).

- [ ] **Step 8: Update `nav.test.ts` if needed**

Run `npm test -- nav` first. If a test asserts the `billing` item is `soon: true`, update it to `false` (or move it to the list of live items). If no such assertion exists, leave the test file untouched.

- [ ] **Step 9: Frontend gate**

Run (from `src/AFK4.Platform.Web`): `npm run build` then `npm test`
Expected: `tsc -b` + `vite build` succeed; all Vitest suites pass.

- [ ] **Step 10: Commit**

```bash
git add src/AFK4.Platform.Web/src/App.tsx src/AFK4.Platform.Web/src/club/nav.ts src/AFK4.Platform.Web/src/club/nav.test.ts
git commit -m "feat(platform-web): wire /club/billing route + flip nav soon flag"
```

---

## Final verification

- [ ] **Backend:** from repo root, `dotnet build` then `dotnet test` — 0 errors, all green (baseline + 6 new).
- [ ] **Frontend:** from `src/AFK4.Platform.Web`, `npm run build` then `npm test` — both green.
- [ ] **Manual seam check:** `/club/billing` reachable for an owner session; nav item no longer shows the "soon" badge; non-owner sessions don't see the item (`ownerOnly`); cross-org subscription/invoice requests are 403 (covered by Task 3).
- [ ] Then use **superpowers:finishing-a-development-branch** to complete the work.

## Spec traceability

- Spec §3 "Club-side" endpoints (`GET /api/organizations/{orgId}/subscription`, `/invoices`, perm `billing.subscription.view`, `RequireOrganizationPermission`) → Tasks 1–3.
- Spec §4.5 "/club/* Биллинг — flip `clubNav` billing to soon:false; read-only owner view: current plan, status, current period, next-invoice date, limits, invoice history; owner-only" → Tasks 4–9.
- Spec §5.7 "Plan 7 — Club-side billing: org-scoped endpoints + /club/billing screen; flip the soon flag" → entire plan.
