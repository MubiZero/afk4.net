# AFK4 Current Progress Snapshot

Last updated: 2026-07-14

## Purpose

Compact current-state snapshot for new sessions — short enough to read every
time. Detailed historical notes and shipped implementation plans/specs live in:

- `docs/archive/progress/`
- `docs/archive/superpowers/plans/` and `docs/archive/superpowers/specs/`

Use archives only when historical evidence or old implementation context is
needed.

## Current Product Direction

- AFK4 is a cloud-first SaaS platform for computer clubs.
- Day-to-day club operations run in the native Windows Operator App (a .NET
  shell hosting a WebView2 React/TypeScript UI).
- Platform-owner/support operations run in the browser SaaS Control Plane
  (`AFK4.Platform.Web`, one SPA with `VITE_AUDIENCE` admin/club builds).
- Players have a self-service shell + installable PWA portal (PIN/QR, balance,
  self-extend, online top-up).
- Backend is a .NET 10 ASP.NET Core modular monolith on PostgreSQL.
- Gaming PCs run the Windows Agent Service + Player Shell; manager workstations
  run the Operator App.

## Navigation

- Live plan/spec indexes: `docs/superpowers/plans/README.md`,
  `docs/superpowers/specs/README.md`.
- Architecture source of truth:
  `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`.
- Operational/production roadmap: `docs/roadmap/production-readiness.md`.

## Implemented (high level)

The full platform-web redesign (both admins), SP3 admin control plane +
SaaS billing, and the entire SP4 wave are implemented and merged to `main`:

- **Counter-loop / postpaid checkout** — open-tab postpaid, credit limits +
  auto-protection, session checkout links.
- **Anti-fraud controls** — manager review/approval, caps, daily owner summary.
- **Offline-resilience** — Agent grace mode, offline lease extension, command
  + billing outbox, adaptive heartbeat.
- **Customer portal (PWA) + customer shell** — player auth, self-service,
  online top-up.
- **Notifications backbone** — MailKit SMTP transport, dispatcher, outbox,
  contact fields + preferences; staff/owner password-reset backend.
- **Localization** — ru/en/tg catalog (`locales/*.json` + `packages/i18n`),
  per-branch locale.
- **Realtime-consistency** — SignalR device/operator clients, optimistic
  `Version`/409 on floor-map edits.
- **dcgate payments** — multi-tenant per-branch payment cards, AES-GCM secret
  storage, HMAC-verified webhook, owner card-onboarding cabinet (Subsystem A +
  B). Note: dcgate is player top-up/payments, separate from SaaS billing.

The Operator commerce/booking completion wave is merged to `main` and deployed
to Coolify staging:

- **Player Shop financial integrity** — new orders settle atomically as linked,
  idempotent paid POS sales with wallet, open-shift, receipt, immutable inventory
  cost, cancellation/refund, and sales-report COGS projections. A real PostgreSQL
  serializable-concurrency test deterministically holds both initial settlements
  after each reads the final stock unit and before commit; one transaction commits,
  while the other retries and returns `out_of_stock`. Whole-branch hardening now
  also preserves immutable stock/cost/currency snapshots through session checkout
  and refunds, uses the reserved `Player Shop` actor, and translates Shop
  transition/cancellation/refund save conflicts without partial finance. Product
  currency updates and every first inventory/sale-history writer now share a
  PostgreSQL serializable protocol; a deterministic first-sale/currency-update
  race proves that incompatible currencies cannot both commit.
- **Operator POS and inventory completion** — clearer price/balance/stock state,
  stable order disclosure motion, one shared Map/POS payment form, atomic mixed
  cash/wallet settlement, immutable original-mix refunds, linked ledger entries,
  and preserved cash-journal/receipt/anti-fraud access.
- **Operator booking and session start** — shared linked-client selection,
  Ctrl/Command multi-seat booking independent of current seat health, explicit
  pending confirmation, optimistic reservation versions, and one atomic,
  idempotent session start linked back to the confirmed reservation. PostgreSQL
  overlap tests prove one effect set for concurrent reservation commands and for
  reservation start racing an ordinary start on the same seat; rollback and audit
  failures leave no partial session, billing, lease, command, or reservation state.
- **Native Operator P0 day-flow gate** — a real Windows WPF/WebView2 host completed
  sign-in, shift open/close, club-client selection, prepaid session start, confirmed
  reservation start, mixed cash/wallet POS payment, receipt refund, restored stock,
  zero-bill prepaid checkout, forced reload, and authoritative reconnect against
  Coolify staging. The smoke found and fixed missing SignalR handlers on secondary
  Operator connections, a cross-type `POS-*` receipt-number collision, and prepaid
  session time being charged again at checkout. Live PostgreSQL reconciliation proved
  sequential `POS-...-0001/0002/0003` receipts for the mixed sale and session checkouts;
  the final fresh prepaid session produced exactly one `gameplay_charge=-300`, no
  checkout payment/debt entries, and a zero checkout quote.

Plus the earlier base: identity/tenancy/RBAC/audit, devices/floor-map, owner-code
enroll, session lifecycle + leases, ledger/POS/shifts/reports, update publishing
+ rollout, and the Agent/Setup-Wizard/Player-Shell/packaging stack.

## Latest Verification

- `dotnet restore AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false`
  and the matching full solution build passed with 0 warnings and 0 errors.
- Shared contracts passed 129/129. The complete Platform API suite passed
  1401/1401 with no skips against isolated commerce, POS, and reservation schemas
  on PostgreSQL 17.10, including deterministic settlement/refund, inventory/
  currency, reservation-start, rollback, and cross-command concurrency tests.
  The full solution build passed with 0 warnings and 0 errors.
- Operator Web passed 735 tests across the component/model and App integration runs;
  the generated ru/en/tg catalog check passed 23/23 and the production build
  completed. Existing React test diagnostics, SignalR annotation warnings, and
  the large-chunk warning remain non-failing.
- Platform Web passed 381/381 Bun tests and its production build; its existing
  large-chunk warning remains.
- Player Shell Web passed 51/51 Bun tests and its production build.
- GitHub `Package Smoke` run `29326881732` and the migration-gated `Coolify
  Staging Deploy` run `29327547027` passed for merge commit `498b7b83`. The
  staging database contains all 69 migrations through
  `20260714130000_VersionReservationsAndLinkSessions`, and the deployed API
  container plus public `/api/health` check are healthy.
- P0 fixes passed latest-head Windows PR Verification in runs `29334853943` and
  `29336317700`, merged through PRs `#124` and `#125`, and deployed from merge
  commits `2922dbd2` and `99f43338`. Coolify staging deploy runs `29335387633`
  and `29337673756` completed with public health verification; the second push
  deploy used one builder and completed without the transient duplicate-worker
  collision seen on the first push attempt.
- The Linux full-solution test attempt passed Platform API 1288 tests (one
  explicit PostgreSQL-env skip) and all other portable suites, but cannot run
  Operator/Player Shell .NET Windows testhosts because `Microsoft.WindowsDesktop.App`
  has no Linux runtime. Twenty-six Agent packaging tests also remain Windows-only
  in this environment because they invoke PowerShell/Windows release tooling;
  their projects compile successfully and require a Windows verification run.

## Known Gaps

- **Per-environment SMTP config** still needs the user's real connection
  details wired into `NotificationOptions`.
- **Operator entity search** is still deferred: the command palette navigates
  between workspaces but does not yet search clients, seats, reservations,
  orders, or receipts.
- **Remaining Windows evidence** is narrower: repeat the Operator pass on a clean
  `manager_workstation` install at 100%/125% scaling and run the physical Windows
  10/11 gaming-PC smoke for lock/unlock enforcement, reboot recovery, and
  role-aware update/rollback.
- **Pre-production release decisions** remain: Authenticode custody, production
  object store/CDN, presigned upload automation, package-registration
  credentials, staging secret rotation, backup/restore ownership — tracked in
  `docs/roadmap/production-readiness.md`.

## Recommended Next Work

1. Repeat the now-passing Operator day flow from a clean `manager_workstation`
   install and record 100%/125% scaling plus keyboard-focus evidence.
2. Run the physical gaming-PC Agent/Shell smoke, then add permission-aware entity
   search to the command palette if it does not expose a higher-priority gap.
3. Wire real per-environment SMTP settings and work through the remaining
   pre-production decisions in the production-readiness roadmap.
