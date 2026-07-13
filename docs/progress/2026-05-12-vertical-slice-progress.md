# AFK4 Current Progress Snapshot

Last updated: 2026-07-13

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
- **Player Shop financial integrity** — new orders settle atomically as linked,
  idempotent paid POS sales with wallet, open-shift, receipt, immutable inventory
  cost, cancellation/refund, and sales-report COGS projections. A real PostgreSQL
  serializable-concurrency test proves exactly one winner for the last stock unit.

Plus the earlier base: identity/tenancy/RBAC/audit, devices/floor-map, owner-code
enroll, session lifecycle + leases, ledger/POS/shifts/reports, update publishing
+ rollout, and the Agent/Setup-Wizard/Player-Shell/packaging stack.

## Latest Verification

- `dotnet restore AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false`
  and the matching full solution build passed with 0 warnings and 0 errors.
- Shared contracts passed 125/125; affected Platform commerce/report suites
  passed 273 tests with one expected environment-gated PostgreSQL skip. The
  same concurrency test passed 1/1 against an isolated temporary
  `afk4_commerce_test` PostgreSQL 17 database before that container was removed.
- Player Shell Web passed 51/51 Bun tests and its production build.
- The Linux full-solution test attempt passed Platform API 1288 tests (one
  explicit PostgreSQL-env skip) and all other portable suites, but cannot run
  Operator/Player Shell .NET Windows testhosts because `Microsoft.WindowsDesktop.App`
  has no Linux runtime. Twenty-six Agent packaging tests also remain Windows-only
  in this environment because they invoke PowerShell/Windows release tooling;
  their projects compile successfully and require a Windows verification run.

## Known Gaps

- **FE forgot/reset-password screen** is still a placeholder
  (`ReservedAuthPage`) even though the backend reset path is wired — build the
  form against the existing backend.
- **Per-environment SMTP config** still needs the user's real connection
  details wired into `NotificationOptions`.
- **Smoke tests** on staging are deferred until the owner says go (incl. the
  `manager_workstation` clean-VM smoke repeat and physical Windows 10/11 smoke:
  lock/unlock enforcement, reboot recovery, role-aware updates/rollback).
- **Pre-production release decisions** remain: Authenticode custody, production
  object store/CDN, presigned upload automation, package-registration
  credentials, staging secret rotation, backup/restore ownership — tracked in
  `docs/roadmap/production-readiness.md`.

## Recommended Next Work

1. Build the FE forgot/reset-password screen on the existing backend and wire
   per-env SMTP config.
2. Run the staging smoke suite when the owner gives the go.
3. Work through the pre-production release decisions in the production-readiness
   roadmap.
