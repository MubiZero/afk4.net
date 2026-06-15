---
name: platform-web-redesign
description: "Full non-MVP redesign of both admins — DONE and merged to origin/main. File now kept for durable engineering patterns (money 100× pitfall, feature shape, bun toolchain, backend/test conventions)."
metadata:
  node_type: memory
  type: project
  originSessionId: e1eb7edc-21e1-4bb7-bc17-823ddeafd946
---

Full (non-MVP) redesign of **AFK4.Platform.Web** — platform admin (`/admin/*`, `platform.afk4.staging.mubi.dev`) and club console (`/club/*`, `app.afk4.staging.mubi.dev`). One SPA, `VITE_AUDIENCE` builds (`all|admin|club`). "Calm SaaS" (indigo), light default + dark toggle, i18n RU primary / EN fallback. Module roots `src/club/*` and `src/platform/*` mirror each other (platform must NOT import `@/club`).

**Status: COMPLETE and merged to origin/main.** All three sub-projects done (foundation+shell+overview; full `/club` owner console; `/admin` control plane + billing — all 7 SP3 plans), plus auth/onboarding UX fixes, bun migration, and the whole SP4 wave (counter-loop, anti-fraud, offline, customer portal/shell, notifications, localization, realtime) and the dcgate payments subsystem. **Workflow is PR-based on the MubiZero remote** (no longer "local-only"). Smoke tests still deferred until owner says go.

## Billing design (locked, Tier B — manual/offline, no payment provider for SaaS billing)
`TenantSubscriptionEntity` is the **source of truth** for plan/status; `OrganizationEntity.PlanCode/SubscriptionStatus/LimitsJson` are synced on every subscription write (so `TenantSuspensionMiddleware` keeps working). Plan catalog PK = `PlanCode` (seeded starter 290000 / growth 790000 / scale 1990000 RUB monthly). Invoices: global-sequential `Number`, `Kind` = subscription|proration; `InvoiceGenerationHostedService` issues per cycle hourly; proration = upgrades only, due = issued+7d. Legacy `PATCH /tenants/{id}/plan` is fully retired — subscription is the only plan write-path. (`PATCH /plans/{code}` = plan **catalog**, unrelated, kept.) Club-side billing is read-only, owner-only (`billing.subscription.view`), org-scoped endpoints with an IDOR guard (route orgId must == `StaffContext.OrganizationId`). Note: dcgate (separate) is the **player top-up / payments** subsystem, unrelated to this SaaS billing.

## Durable patterns (reuse, don't reinvent)
- **Frontend feature shape** (per `src/club/overview`): pure `*Model.ts` builder + `use*` hook (discriminated-union `loading|error|ready`, all carry `retry`, `useRef` client, deps `[id,tick]`) + presentational screen. Primitives `@/components/ui/*` + `@/components/ui/states` (LoadingCards/ErrorState/EmptyState) + `@/components/shared/ConfirmDialog` + `@/components/shell/AppShell`. `ClubArea`/`PlatformArea` live inline in `App.tsx`.
- **Money (caused a 100× bug):** backend DTOs carry integer **minor** units (e.g. `amountMinorUnits`), but `useI18n().formatCurrency(amount, code)` formats `amount` as **MAJOR** units (Intl currency, `maximumFractionDigits:0`, NO ÷100). You MUST convert at the UI boundary: `formatCurrency(minorToMajor(x.amountMinorUnits), code)` and enter prices via `majorToMinor(...)`. Helpers in `src/AFK4.Platform.Web/src/club/money.ts` (`minorToMajor=÷100`, `majorToMinor=Math.round((v+EPSILON)*100)`) — import via `@/club/money`. Per-task reviews missed this; only a holistic seam-review caught it.
- **i18n**: `useI18n()` flat keys in `src/i18n/messages.ts` (ru block then en block; `MessageKey = keyof messages.ru`); a test enforces ru/en parity. App-wide locale catalog also lives in `locales/{ru,en,tg}.json` + `packages/i18n` (SP4 localization).
- **Frontend gates**: run BOTH `bun run build` (`tsc -b`) AND `bun run test`. **Toolchain = Bun** (`bun install`/`bun run`; lockfile `bun.lock`; deploy Dockerfile uses `oven/bun`). Tests run via `bun run test` — see [[frontends-on-bun-test]] for the exact runner setup.
- **Backend**: platform endpoints map directly on `app` (no MapGroup); auth via `authorizationService.RequirePermission(PlatformAdminPermissionNames.X)` (perms in `AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`, role→perm map in `Platform/Identity/PlatformAdminPermissionCatalog.cs`); staff perms in `AFK4.Shared.Contracts/.../StaffPermissionNames.cs` + `Identity/PermissionCatalog.cs`. DI `AddScoped<I,EfImpl>()` in `Program.cs`. Tests: xUnit + `Assert.*` (no FluentAssertions), `PlatformApiFactory`/`StaffAuthTestHelper`/`PlatformAdminTestHelper`. **Org-scoped endpoints**: `RequireOrganizationPermission` does NOT validate route org → handlers MUST add an IDOR guard and use the TRUSTED `StaffContext.OrganizationId`.
- **`src/preview/DemoApp.tsx`** is the user's UNTRACKED scratch — never `git add` it; keep it compiling in the working tree if a shared shell prop contract changes, but leave it untracked.

See [[ux-audit-roadmap]] for the SP4 feature wave, [[coolify-reference]] for staging, [[email-server-available]] for the password-reset gap.
