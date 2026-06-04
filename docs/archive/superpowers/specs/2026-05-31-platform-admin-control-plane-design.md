# Platform Admin Control Plane (`/admin/*`) — Design Spec

**Date:** 2026-05-31
**Sub-project:** #3 of the Platform.Web redesign (after #1 foundation+Club Overview and #2 full `/club/*` console).
**Status:** Approved design; implementation proceeds as a sequence of plans (Plan 1 first).

## 1. Goal & context

Bring the owner-facing SaaS **control plane** (`/admin/*`, host `platform.afk4.staging.mubi.dev`) up to the same "Calm SaaS" bar already achieved for the `/club/*` console, **and** add a real subscription/invoicing backend so the control plane can actually manage tenant billing — the "club Billing" that was deferred from sub-project #2.

This is a **full-stack** sub-project (decided with the owner): new backend (ASP.NET minimal APIs + EF entities + a background job + xUnit) **plus** the frontend redesign mirroring the `/club/*` architecture.

### Current state (verified 2026-05-31)
- The `/admin/*` UI renders inline in `App.tsx:256-291` with legacy CSS classes (`TenantList`, `TenantDetail`, `StatusControl`, `PlanControl`, `LimitsControl`, `OwnerInvitesSection`, `SupportNotesSection`, `HealthSection`, `NewTenant`, `SignIn`, `AcceptInvite`). No `AppShell`, no nav, no shared design-system usage. There is **no** `PlatformArea` wrapper (unlike `ClubArea`).
- Backend `/api/platform/*` surface is exactly 17 routes: auth (sign-in/refresh/sign-out); tenants (GET list, GET one, POST create, PATCH status, PATCH plan, PATCH limits); GET health; owner-invites (GET/POST/revoke/accept); support-notes (GET/POST/PATCH). **No** metrics/dashboard, **no** invoices/subscriptions (monetary), **no** plan catalog endpoint, **no** club-side billing endpoint.
- "Subscription" today = `OrganizationEntity.PlanCode` (string) + `SubscriptionStatus` (enum: trial/active/past_due/cancelled). Plan codes (`starter`/`growth`/`scale`) are hardcoded constants with no prices and no catalog table. `TenantSuspensionMiddleware` reads the org status.
- `clubNav` already has a `billing` item (`/club/billing`, ownerOnly, `soon: true`) — a placeholder. This sub-project makes it real (read-only).

### Design system & patterns to reuse (do not reinvent)
- Primitives: `@/components/ui/*` (`card`, `button`, `input`, `select`, `badge`, `table`, `dialog`, `sheet`, `tabs`, `switch`, `toast`, `checkbox`, `skeleton`), `@/components/ui/states` (LoadingCards/ErrorState/EmptyState), `@/components/shared/ConfirmDialog`, `@/components/shell/AppShell`.
- Feature shape (per `src/club/overview`): pure view-model builder (`*Model.ts`) + `use*` hook (discriminated-union state `loading|error|ready`, all carry `retry`) + presentational screen. Tests: Vitest `globals:false` (`import { it, expect, vi } from 'vitest'`), `renderHook`/`render`, `vi.fn().mockResolvedValue`.
- i18n: `useI18n()` (`t`, `formatNumber`, `formatCurrency`, `formatDate`), flat keys in `src/i18n/messages.ts` (RU primary, EN fallback). New keys under `platform.*` / `admin.*`.
- Money everywhere = **minor units + currencyCode**.
- Destructive/financial actions: `ConfirmDialog` (reason + server-confirmed only, no optimistic success, toast on result). Idempotency via existing `IPlatformIdempotencyStore`; audit via `IAuditRecordWriter`.

## 2. Frontend architecture

New module **`src/platform/`** mirroring `src/club/` (per-feature folders: `overview/`, `tenants/`, `billing/`, `profile/`, plus `nav.ts`, `metricsModel.ts` etc.).

New **`PlatformArea`** component in `App.tsx` (analogous to `ClubArea`), wrapping `AppShell` with the platform nav and **no branch switcher** (the platform admin has no branch context). It owns the `/admin/*` route-union rendering and replaces the inline legacy block at `App.tsx:256-291`.

**Navigation (`src/platform/nav.ts`):**
- Group "Control plane": **Обзор** `/admin`, **Тенанты** `/admin/tenants`, **Биллинг** `/admin/billing`.
- Group "Аккаунт": **Профиль** `/admin/profile`.

Health and support-notes are surfaced inside the tenant detail, not as separate nav items.

`SignIn` and `AcceptInvite` stay as-is (out of redesign scope, like the club login).

## 3. Backend data model & endpoints (new)

Project `AFK4.Platform.Api`. Money = minor units + currencyCode. **Subscription becomes the source of truth** for plan/status; the denormalized `OrganizationEntity.PlanCode/SubscriptionStatus` are kept in sync (so `TenantSuspensionMiddleware` keeps working unchanged).

### Entities (EF, new migration)
- **`SubscriptionPlanEntity`** (plan catalog): `PlanCode` (unique), `Name`, `PriceMinorUnits`, `CurrencyCode`, `BillingInterval` (`monthly`/`yearly`), limits (`MaxBranches`, `MaxDevicesPerBranch`, `MaxConcurrentSessions`, `MaxStaffUsersPerBranch`), `IsActive`, `SortOrder`. Seeded with `starter`/`growth`/`scale`. Assigning a plan to a tenant copies the catalog limits into the org limits.
- **`TenantSubscriptionEntity`** (one active per org): `OrganizationId`, `PlanCode`, `Status` (trial/active/past_due/cancelled), `CurrentPeriodStartUtc`, `CurrentPeriodEndUtc`, `NextInvoiceUtc`, `AmountMinorUnits`, `CurrencyCode`, `CancelAtPeriodEnd`, timestamps.
- **`InvoiceEntity`**: `InvoiceId`, `OrganizationId`, `Number` (sequential), `PeriodStartUtc`, `PeriodEndUtc`, `IssuedAtUtc`, `DueAtUtc`, `AmountMinorUnits`, `CurrencyCode`, `Status` (issued/paid/void/overdue), `PaidAtUtc`, `VoidedAtUtc`, `VoidReason`, `Description`. Single amount per invoice (no line items); a mid-cycle plan change emits a separate proration invoice.

### Background job
- **`InvoiceGenerationHostedService`** (`BackgroundService`, hourly tick): for each subscription with `Status = active` and `NextInvoiceUtc <= now`, issue an invoice for the current period, then advance `CurrentPeriod*` / `NextInvoiceUtc` by the interval; flip `issued` → `overdue` once past `DueAtUtc`. Idempotent (per `(subscriptionId, periodStart)` via `IPlatformIdempotencyStore`); writes audit records.
- **Proration**: changing a plan mid-cycle issues a one-off adjustment invoice for the prorated difference (by remaining days in the period); the subscription's amount/plan update from that point.

### Endpoints (minimal API, `Program.cs`)
Platform-admin (perm constants in `PlatformAdminPermissionNames`, add new where needed):
- `GET /api/platform/metrics` — aggregate KPIs.
- `GET /api/platform/plans`, `POST /api/platform/plans`, `PATCH /api/platform/plans/{planCode}` — plan catalog.
- `GET /api/platform/tenants/{id}/subscription`, `PATCH /api/platform/tenants/{id}/subscription` (change plan / interval / cancel-at-period-end). This supersedes the role of the existing `PATCH .../plan`.
- `GET /api/platform/tenants/{id}/invoices`; `POST .../invoices/generate` (manual trigger); `POST .../invoices/{invoiceId}/mark-paid`; `POST .../invoices/{invoiceId}/void`.

Club-side (org-scoped staff session, new permission `billing.subscription.view`, `RequireOrganizationPermission`):
- `GET /api/organizations/{orgId}/subscription` — own subscription.
- `GET /api/organizations/{orgId}/invoices` — own invoices (read-only).

All new endpoints get xUnit coverage alongside the existing `tests/AFK4.Platform.Api.Tests/Platform/*EndpointTests.cs` (harness `PlatformApiFactory`).

## 4. Screens

### `/admin/*`
1. **Обзор (Overview)** — KPIs from `GET /metrics`: tenants by status & by plan, MRR (sum of active subscriptions, normalized to monthly), outstanding/overdue invoices, new tenants. "Attention" list: suspended / past_due tenants and overdue invoices.
2. **Тенанты (Tenants)** — list (`Table`, search + filter by status/plan) + detail in a `Sheet` drawer with sections: status (ConfirmDialog), subscription/plan (change plan, cancel), limits, owner-invites, support-notes, health, the tenant's invoices.
3. **Биллинг (Billing)** — cross-tenant, `Tabs`: **Подписки** (all subscriptions) / **Инвойсы** (filter by status; mark-paid / void via ConfirmDialog; manual generate) / **Тарифы** (plan catalog: price / interval / limits, create + edit).
4. **Профиль (Profile)** — read-only platform-admin profile (displayName, roles, grouped permissions, sign-out). Mirror of club `ProfileScreen`.

### `/club/*`
5. **Биллинг (Billing)** — flip `clubNav` `billing` to `soon: false`. Read-only owner view: current plan, status, current period, next-invoice date, limits, invoice history. Owner-only (`billing.subscription.view`).

## 5. Decomposition (the spec covers all; Plan 1 is implemented first)

1. **Plan 1 — Foundation + Platform Overview.** `src/platform/` module, `PlatformArea` + `nav.ts` + `/admin/*` route-union, backend `GET /api/platform/metrics` (aggregate over existing org/branch data — billing-derived KPIs like MRR/outstanding arrive once billing lands), Overview screen. Mirror of club Plan 1.
2. **Plan 2 — Tenants list + detail** redesigned on the design system using existing endpoints; delete legacy `TenantList`/`TenantDetail`/`*Control`/`*Section`.
3. **Plan 3 — Billing backend**: plan catalog + subscription + invoice entities + proration + `InvoiceGenerationHostedService` + endpoints + xUnit. Migrate `/plan` semantics into subscription.
4. **Plan 4 — Billing admin UI**: the Billing area (Подписки/Инвойсы/Тарифы) + subscription/invoice sections in tenant detail.
5. **Plan 5 — Owner-invites + support-notes** sections redesigned in tenant detail.
6. **Plan 6 — New tenant flow + Platform Profile + delete remaining legacy admin components.**
7. **Plan 7 — Club-side billing**: org-scoped endpoints + `/club/billing` screen; flip the `soon` flag.

## 6. Testing & gates
- Frontend: Vitest (`globals:false`) for models, hooks, and screens (loading/error/ready + retry), as in `src/club/*`. Build gate = `tsc -b` (esbuild/vitest skips type-checks).
- Backend: xUnit (`tests/AFK4.Platform.Api.Tests/Platform/...`) via `PlatformApiFactory`; `dotnet build` + `dotnet test`.

## 7. Defaults (locked unless changed)
- Plan currency default **RUB**; default interval **monthly**.
- Proration = one-off adjustment invoice on mid-cycle plan change.
- Invoice numbering = global sequential.
- `TenantSubscriptionEntity` is the source of truth; `OrganizationEntity.PlanCode/SubscriptionStatus` kept in sync.
- `SignIn` / `AcceptInvite` unchanged.

## 8. Out of scope
- External payment provider / real charging / webhooks (no Stripe etc.).
- Invoice line items (single amount per invoice).
- Usage-metered billing.
