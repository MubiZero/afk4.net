# AFK4 Current Progress Snapshot

Last updated: 2026-05-26

## Purpose

This is the compact current-state snapshot for new sessions. It should stay
short enough to read every time. Detailed historical notes were moved to:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
- `docs/archive/progress/2026-05-26-progress-snapshot-before-doc-hygiene.md`
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

## Active Plans

- `docs/superpowers/plans/2026-05-23-saas-control-plane-tenant-onboarding.md`
  is the approved Control Plane tenant-onboarding plan.
- `docs/superpowers/plans/2026-05-24-afk4-club-self-service-onboarding.md`
  is the active follow-on onboarding plan. Slices 1.1 through 3.5 are complete
  in local implementation state.
- `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`
  is the Operator App runtime migration plan.
- `docs/superpowers/plans/2026-05-23-operator-app-pilot-hardening.md`
  is the focused Operator App pilot-hardening plan.
- `docs/superpowers/plans/2026-05-24-afk4-roadmap-post-onboarding.md`
  tracks post-onboarding roadmap direction.

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

- 2026-05-26 local `main` at `a28c3e4`:
  - `dotnet build .\AFK4.sln -p:EnableWindowsTargeting=true ...` passed with
    0 warnings and 0 errors.
  - `dotnet test .\AFK4.sln --no-restore -p:EnableWindowsTargeting=true ...`
    passed 1053/1053 tests.
  - `main` was pushed to `origin/main`.
- Latest recorded clean Windows 11 VM Agent baseline: internal Agent MSI
  `0.1.29` from `Package Smoke` run `26442315418`; VM2 applied the rollout,
  rebooted, kept `AFK4.Agent.Service` running, and did not reopen Setup Wizard.
- Slice 3.5 local packaging cleanup:
  - `actionlint` passed for client/package/deploy/PR workflows.
  - focused release automation tests passed 59/59.
  - default package build for `0.1.35-slice35` produced Operator App, Agent,
    and Player Shell MSI artifacts only; legacy gaming-PC MSI/setup executable
    were absent.
- 2026-05-26 `manager_workstation` VM smoke follow-up on branch
  `codex/manager-workstation-operator-env`:
  - Clean Windows 11 enrollment as `manager_workstation` completed, Agent
    Service ran, and staging rollout installed Operator App, but smoke exposed
    four local blockers: missing Operator App platform URL/bootstrap context, a
    framework-dependent Operator App MSI, WebView2 data directory creation under
    Program Files, and Operator App reaching connection resolution without the
    organization/branch IDs written by Setup Wizard.
  - Focused verification passed:
    `AFK4.Operator.App.Tests` 206/206, targeted Operator App bootstrap/options
    checks 12/12, `AFK4.SetupWizard.Tests` 11/11, and focused
    `AFK4.Agent.Service.Tests` packaging/update checks 3/3.
  - Local package build
    `scripts/build-client-packages.ps1 -Version 0.1.33-manager-env -Channel internal`
    produced `afk4-agent-0.1.33-manager-env-internal.msi`
    (`57191741` bytes,
    SHA256 `4DFA43E3994ADA195B0748C3FF05CD5CB9D354045EAC009252E4FB436FCEC393`)
    and `afk4-operator-app-0.1.33-manager-env-internal.msi`
    (`54165568` bytes,
    SHA256 `8013D8DDF1BC9CB8CDFB6D4247276D366C93B1C259FC755AB97C04E9338E5635`),
    plus Player Shell MSI artifacts.
- 2026-05-26 `manager_workstation` seatless-enrollment follow-up on branch
  `codex/manager-workstation-seatless-enroll`:
  - Fixed the remaining clean-VM model bug: `manager_workstation` enrollment no
    longer requires or creates a floor-map seat assignment. Gaming PCs still
    require a free seat.
  - Setup Wizard now selects role before optional seat selection; manager
    workstations enroll directly after branch selection, while gaming PCs move
    to free-seat selection or missing-seat creation.
  - Floor-map projection and React realtime state now ignore
    non-`gaming_pc` device assignments/statuses so old manager assignments do
    not appear as ready gaming PCs.
  - Focused verification passed:
    `AFK4.SetupWizard.Tests` 13/13, install contract serialization 4/4,
    install/floor-map Platform API tests 19/19, Operator App Web
    `floorMapState`/`operatorRealtime` tests 8/8, Operator App Web production
    build, and local client package build.
  - Full solution verification also passed: `dotnet build .\AFK4.sln` with
    0 warnings/0 errors and `dotnet test .\AFK4.sln --no-restore` with
    1061/1061 tests passing.
  - Local package build
    `scripts/build-client-packages.ps1 -Version 0.1.36-manager-seatless -Channel internal`
    produced `afk4-agent-0.1.36-manager-seatless-internal.msi`
    (`57195837` bytes,
    SHA256 `3270DF1D85F84DA3CACC1EAE0922D1DB6862279BD904D44856BF8BF5DC4622E3`)
    and `afk4-operator-app-0.1.36-manager-seatless-internal.msi`
    (`54145088` bytes,
    SHA256 `02ECD90E788F4F0869C7B1559F366E11848BDC9613F77F2EF6B80D2E5F467B0D`),
    plus Player Shell MSI artifacts.

## Known Gaps

- `manager_workstation` role smoke has local fixes and a
  `0.1.36-manager-seatless` MSI for the clean-VM blockers; staging backend must
  be updated for nullable install `SeatId` before the new Wizard flow can be
  smoked against `afk4.staging.mubi.dev`. Existing staging smoke data also
  needs cleanup of mistakenly created manager seats/assignments, and stale
  deleted-VM devices still need heartbeat/offline threshold hardening.
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

1. Deploy the seatless install-enrollment backend to staging, clean mistaken
   manager-workstation seat data from the smoke branch, then rerun the
   `manager_workstation` clean Windows VM smoke with the
   `0.1.36-manager-seatless` Agent MSI.
2. Continue Operator App staging hardening using
   `docs/superpowers/plans/2026-05-23-operator-app-pilot-hardening.md`.
3. Repeat real-device Windows smoke on physical hardware when available.
