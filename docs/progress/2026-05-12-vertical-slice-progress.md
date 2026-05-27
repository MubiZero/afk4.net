# AFK4 Current Progress Snapshot

Last updated: 2026-05-27

## Purpose

This is the compact current-state snapshot for new sessions. It should stay
short enough to read every time. Detailed historical notes were moved to:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
- `docs/archive/progress/2026-05-26-progress-snapshot-before-doc-hygiene.md`
- `docs/archive/progress/2026-05-27-context-refresh-archived-details.md`
- `docs/archive/superpowers/plans/`
- `docs/archive/superpowers/specs/`

Use archives only when historical evidence or old implementation context is
needed.

## Current Product Direction

- AFK4 is a cloud-first SaaS platform for computer clubs.
- MVP day-to-day club operations are in the native Windows Operator App.
- MVP platform-owner/support operations are in an internal browser-based SaaS
  Control Plane.
- Customer browser operational admin is not the primary club UI.
- Backend is a .NET 10 ASP.NET Core modular monolith on PostgreSQL.
- Gaming PCs are Windows 10/11 and run the Windows Agent Service plus Player
  Shell.
- Operator App target runtime is a native .NET Windows shell with WebView2 and
  React/TypeScript UI.

## Active Plans And Navigation

Use `docs/superpowers/plans/README.md` for the live plan index and
`docs/superpowers/specs/README.md` for active architecture specs.

Active implementation plans:

- `docs/superpowers/plans/2026-05-24-afk4-club-self-service-onboarding.md` -
  Slices 1.1 through 3.5 are implemented on `main`; Slice 4 public landing work
  is next.
- `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md` -
  Operator App runtime migration and parity plan.
- `docs/superpowers/plans/2026-05-23-operator-app-pilot-hardening.md` -
  Operator App staging and pilot-hardening follow-up.

Roadmap/reference:

- `docs/superpowers/plans/2026-05-24-afk4-roadmap-post-onboarding.md` is a
  roadmap, not an implementation plan.
- `docs/superpowers/plans/2026-05-23-saas-control-plane-tenant-onboarding.md`
  is a completed Control Plane implementation record and deferred-hardening
  reference.

## Implemented Capabilities

### Backend Platform

- Identity, tenancy, staff sign-in/refresh, predefined MVP roles/permissions,
  branch-scoped authorization, audit, diagnostics, and reports.
- EF Core/Npgsql persistence and migrations for identity, tenancy, devices,
  layout, sessions, billing, POS, shifts, updates, audit, diagnostics, and
  reports.
- Branch profile, floor map, zones/seats, device inventory/detail, installed
  app reporting, device-seat assignment, pending device approval/reject/rename/
  move/remove, and realtime device status broadcast.
- Owner-code install flow: generate/rotate/summary, unauthenticated
  discover/enroll, install seat creation, per-source-IP backoff, audit, tenant
  status checks, and five-failure owner-code revocation.
- Session start/extend/transfer/end, signed leases, heartbeat/reconciliation,
  lock/unlock command planning, duplicate-command suppression, and ending-state
  recovery.
- Immutable ledger wallet/debt/packages, POS catalog/sales/payments/refunds/
  voids/receipts, shifts/cash movements, and CSV reports.
- Update package registration, rollout state, device update check/status, and
  Agent update status tracking.

### SaaS Control Plane / Platform Web

- Platform Web route split exists for `/admin/*`, `/auth/*`, and `/club/*`.
- Admin tenant list/create/detail screens live under `/admin`.
- Public accept-invite and staff sign-in routes call backend auth endpoints and
  store a separate staff session.
- Customer `/club/*` screens include dashboard/install branch overview,
  owner-code generate/rotate, branch settings, ETag floor-map editor,
  devices/pending-device admin, and operators screens.
- `VITE_AUDIENCE` supports separate admin and customer SPA builds.

### Operator App

- Native .NET shell with WebView2/React direction is approved and partly
  implemented.
- Native host owns protected token storage and auth bridge messages.
- WebView2 host now uses a per-user LocalAppData profile folder instead of
  creating browser data beside the installed executable under Program Files.
- React UI gates the console behind staff sign-in, uses typed API clients, and
  avoids browser storage for tokens.
- Primary floor-map UI has backend loading, selected-seat actions,
  permission-aware navigation, billing-mode selection, filters/table view,
  SignalR device status/command-result reloads, active-session ticking, and
  backend-confirmed session commands.
- Existing WPF/MVVM implementation remains migration source for parity areas
  not yet ported.

### Agent, Setup Wizard, Player Shell, Packaging

- Single `AFK4 Agent` MSI installs Agent Service, Setup Wizard, update helpers,
  first-run marker, RunOnce wizard launch, and service registration.
- Setup Wizard supports owner-code discovery/enrollment, branch/floor-map
  discovery, role choice before optional seat selection, missing-seat creation
  for gaming PCs, seatless manager-workstation enrollment, stable device key,
  machine bootstrap environment writing, Operator App base URL and branch
  context bootstrap, and service start after enrollment.
- Agent role-aware update flow installs Player Shell for `gaming_pc` and
  Operator App for `manager_workstation`; Operator App install checks WebView2.
- Standalone Operator App MSI and Player Shell MSI exist; Operator App package
  builds as self-contained x64 for clean Windows manager-workstation installs.
- Default package build now produces only Operator App, Agent, and Player Shell
  MSI artifacts. The legacy coordinated `afk4-gaming-pc` MSI and staging setup
  executable require explicit fallback switches and are not part of the default
  onboarding/publishing path.

## Latest Verification

- Latest full local solution verification was recorded on 2026-05-27 after the
  package-smoke rollout fixes:
  - `ClientReleaseAutomationTests` passed 42/42.
  - Floor-map Platform API tests passed 8/8.
  - `dotnet build .\AFK4.sln` passed with 0 warnings and 0 errors.
  - `dotnet test .\AFK4.sln --no-restore` passed 1064/1064 tests.
- Latest staging/backend evidence:
  - `cac13da` deployed to staging through `Coolify Staging Deploy` run
    `26461039901`; `https://afk4.staging.mubi.dev/api/health` returned
    `status = ok`.
  - Package Smoke run `26507696459` on `120a5a1` passed, published internal
    package version `0.1.33`, created the expected `operator-app` branch
    rollout, and kept Agent Service rollout targeting device-scoped.
- Latest clean Windows endpoint evidence:
  - Windows 11 VM Agent baseline reached internal Agent MSI `0.1.29`; after
    rollout/reboot, `AFK4.Agent.Service` kept running and Setup Wizard did not
    reopen.
  - `manager_workstation` enrollment fixes are implemented on `main` and staged:
    manager workstations enroll without floor-map seats, while gaming PCs still
    require a free seat.
- Verbose branch, artifact, hash, and rollout-id evidence is archived in
  `docs/archive/progress/2026-05-27-context-refresh-archived-details.md`.

## Known Gaps

- `manager_workstation` clean-VM smoke still needs one repeat after cleanup of
  mistakenly created manager smoke seats/assignments. The staging backend and
  Operator App rollout path are ready for that repeat.
- Floor-map reads now apply the branch stale-heartbeat threshold after refresh;
  proactive realtime offline broadcasts and broader inventory/detail
  stale-state cleanup remain hardening.
- Operator App staging hardening remains the highest product-value work after
  onboarding packaging cleanup: run backend-backed staging day flows and remove
  remaining production-visible fixtures/placeholders/raw GUID forms from normal
  operator paths.
- Physical Windows 10/11 smoke is still needed for wider rollout confidence:
  lock/unlock enforcement, reboot recovery, Setup Wizard, role-aware updates,
  and update/rollback.
- Agent hardening still needs rotated device credential consumption, richer
  reboot/lock recovery telemetry, and lease/heartbeat threshold tuning.
- Release decisions remain before commercial production: Authenticode custody,
  production object store/CDN, presigned upload automation, package
  registration credentials, staging secret rotation, and backup/restore
  ownership.

## Recommended Next Work

1. Clean mistaken manager-workstation smoke seat data, then rerun the
   `manager_workstation` clean Windows VM smoke with the current Agent MSI and
   published internal Operator App rollout.
2. Continue Operator App staging hardening using
   `docs/superpowers/plans/2026-05-23-operator-app-pilot-hardening.md`.
3. Repeat real-device Windows smoke on physical hardware when available.
