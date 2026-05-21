# AFK4 Production Readiness Roadmap

Last updated: 2026-05-21

## Purpose

This roadmap tracks what separates the current AFK4 codebase from production.
It is intentionally operational: infrastructure, release gates, security,
backups, device validation, and pilot readiness.

The product scope and architecture decisions remain in:

- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`

The current implementation snapshot remains in:

- `docs/progress/2026-05-12-vertical-slice-progress.md`

## Production Definitions

### Pilot Production

Pilot production means AFK4 can run in one controlled club or lab environment
with close operator/developer supervision.

Minimum bar:

- Platform API deployed to a production-like environment.
- PostgreSQL hosted outside a developer machine.
- TLS, domain, and environment-based secrets configured.
- One Windows test endpoint enrolled with Agent Service and Player Shell. A
  clean Windows 11 VM is acceptable for the pilot gate; physical PC validation
  remains recommended hardening before wider rollout.
- Operator App can perform the core day flow against the deployed backend.
- Backup and restore rehearsal completed once.
- Client MSI installation and update rollout tested on a Windows endpoint.
- Manual/mock payments are acceptable.
- Manual operational setup is acceptable if documented.

### Commercial Production

Commercial production means AFK4 can be given to real clubs without developers
standing next to every critical operation.

Minimum bar:

- Mandatory CI gates and release discipline.
- Staging environment mirrors production closely enough for migration, update,
  and smoke rehearsal.
- Backup retention, encryption, restore ownership, and incident procedure are
  defined and tested.
- Production certificate/signing/CDN/secrets policy is settled.
- Agent lock/reboot/update/rollback behavior is validated on Windows
  endpoints, with physical-device repeats treated as hardening for wider
  rollout.
- Staff, role assignment, layout, device, update, audit, and reporting
  workflows are usable by operators/managers.
- Operational monitoring and support diagnostics are actionable.

## Critical Path To Pilot Production

1. **Staging Infrastructure**

   Deploy the Platform API and PostgreSQL in a production-like environment.
   The first Coolify-managed Linux VPS staging rehearsal was executed on
   2026-05-17 from `codex/coolify-staging-rehearsal`: Coolify built the
   Platform API container from the repository, ran a managed PostgreSQL
   service, applied EF migrations explicitly, and passed backend health/auth
   smoke. The real staging domain `afk4.staging.mubi.dev` resolves to the
   Coolify VPS and passes `/api/health` over trusted TLS. The rehearsal API
   token and staging database/session secrets were rotated after the
   hardening pass. A GitHub Actions `Coolify Staging Deploy` workflow now
   removes the manual Coolify click path for ordinary Platform API staging
   deploys while keeping EF migration changes behind an explicit backup and
   migration confirmation.

2. **CI Gate**

   Use cost-aware GitHub Actions workflows to build and test relevant pull
   requests, run package smoke for client MSI artifacts, and keep release
   packaging manual and guarded. Staging backend deploy is automated through
   Coolify API queue/polling and public health verification after relevant
   `main` pushes. GitHub Actions billing is enabled, but
   workflows must avoid unnecessary manual remote runs, use Windows hosted
   runners only where they add required coverage, cancel stale PR runs, set
   timeouts, and keep artifact retention short. PR #11 merged the cost-aware CI
   gate, and PR #12 opted GitHub JavaScript actions into Node 24 execution.
   GitHub rulesets are not enforced on the current private repository plan, so
   merges must manually follow `AGENTS.md`: the current PR head commit needs a
   green remote `PR Verification Result` before merge.

3. **Windows Endpoint Smoke**

   Enroll a Windows 10/11 test endpoint. Validate device credential auth,
   heartbeat, SignalR commands, session start/end, lease refresh, lock/unlock,
   Player Shell state, installed app report, and diagnostics. A repeatable
   manual staging runbook for physical hardware still exists at
   `docs/operations/real-device-windows-pc-smoke.md`, and the Agent host is
   wired for Windows Service runtime under service name `AFK4.Agent.Service`.
   A staging-only one-click Gaming PC setup executable path now exists for clean
   Windows 11 smoke VMs, and the staging public lease verification key is
   committed for reproducible packaging. One Windows 11 VM passed rebuilt x64
   install/enroll/heartbeat plus session start/end, signed lease, local runtime
   state, and visible Player Shell active/locked evidence. The smoke also
   exposed two hardening gaps: service-started session-0 Shell competition and
   missing `ending` session finalization after accepted lock. Both are now
   mitigated in code on `codex/staging-gaming-pc-bootstrapper`: Agent Service
   Shell auto-start targets the active interactive Windows session with
   session-aware process detection, and accepted/completed lock command results
   or the next heartbeat finalization fallback move sessions to `ended` so the
   seat/device can be reused. After staging was redeployed from that branch,
   the Windows 11 VM reuse smoke passed without SQL cleanup. Corrected remote
   bootstrap `0.1.14` then passed clean Windows 11 VM install/enroll/
   seat-assignment/heartbeat/locked-Shell smoke plus two session start/end
   cycles with seat/device reuse. This is enough to proceed with pilot Operator
   App testing and continued development. Physical Windows 10/11 hardware,
   reboot recovery, and update/rollback repeats remain hardening work before
   wider operational rollout, not blockers for the current pilot/dev cycle.

4. **Backup And Restore Rehearsal**

   Run `docs/operations/postgres-backup-restore.md` against staging data:
   backup, restore into a clean database, apply migrations, start the API, and
   smoke health/auth/floor-map/diagnostics/audit/reports/update status. A
   repeatable helper now exists at `scripts/rehearse-postgres-restore.ps1` for
   the backup, archive-readability, restore, migration update, and table-count
   parts of the rehearsal. A local Docker-based rehearsal passed on
   2026-05-19 against a temporary PostgreSQL 17 container, including API
   health/auth smoke on the restored database. The real Coolify staging
   rehearsal also completed on 2026-05-19: the staging database was temporarily
   exposed through Coolify's TCP proxy, backed up, restored into a temporary
   rehearsal database, migration-checked, smoke-tested through the Platform API,
   cleaned up, and returned to non-public database access.

5. **Signed Client Release Rehearsal**

   Staging now has a temporary pilot update-hosting path using Coolify-hosted
   MinIO at `updates.afk4.staging.mubi.dev`. The package smoke workflow can
   build MSI artifacts, publish signed update metadata to staging MinIO,
   register packages with the staging Platform API, and create an internal
   device rollout. On 2026-05-18, an already enrolled Windows 11 VM installed
   Agent/Shell `0.1.3` through the Agent update pipeline and reported
   `installed` to the backend. Follow-up staging rollouts brought that VM to
   `0.1.7`, verified atomic artifact download/recovery behavior, and fixed the
   backend so older active rollouts are not re-offered to devices that already
   report a newer installed MSI-compatible version. The update epic is closed
   for the current pilot/dev cycle. The staging Gaming PC setup executable
   remains only a bootstrap path for clean machines; commercial production
   still needs final Authenticode/signing custody, production storage/CDN
   policy, and service credentials for package registration. Physical PC
   update/rollback evidence remains broader release hardening. On 2026-05-19,
   `Package Smoke` also began publishing a remote
   clean-machine Gaming PC bootstrap script and `latest.json` manifest to
   staging MinIO. The public latest manifest URL was verified after workflow
   run `26089632552` and points to version `0.1.13`; this removes local file
   copying for clean staging VM bootstrap while keeping already enrolled PCs on
   the signed/internal MSI update rollout path. The first clean VM run against
   version `0.1.13` enrolled and assigned the seat, but exposed a bootstrap/MSI
   sequencing bug: MSI starts `AFK4.Agent.Service` during installation before
   the script had written the enrolled device credential and machine config.
   PR #33 moved that config write before `msiexec.exe`; post-merge `Package
   Smoke` run `26091453388` published corrected bootstrap version `0.1.14`.
   A clean Windows 11 VM then passed remote bootstrap install/enroll/
   seat-assignment/heartbeat/locked-Shell smoke against `0.1.14`, followed by
   two session start/end cycles with visible Player Shell active/locked state
   and seat/device reuse without SQL cleanup. The second session end exposed a
   follow-up backend issue: duplicate lock commands can be planned for one
   session end. PR #38 fixed issue #36 by suppressing duplicate heartbeat and
   reconciliation lock planning when a lock already exists for the same
   device/session. Coolify staging was redeployed to commit
   `ccf938354d7cb86edf2349cf5696a7dd51332136`, and the VM recheck confirmed
   one fresh lock command for one session end before issue #36 was closed.

6. **Pilot Setup Runbook**

   Document exactly how to create the first organization, branch, staff users,
   roles, zones, seats, devices, tariffs, POS products, and update channels for
   a pilot club. Device-seat assignment now has a staff-authorized Platform API
   path and staging setup integration. PRs #23 and #24 added staff user/role
   and layout setup APIs plus a PowerShell pilot setup script that composes
   existing tariff, POS, and device assignment endpoints. The script completed
   against staging on 2026-05-19 using a branch manager account and no direct
   PostgreSQL edits. PR #41 added the minimum Operator App `Settings` ->
   `Pilot Setup` panel for staff, one zone/seats, one tariff/version, one POS
   category/product, and optional already-enrolled device assignment.

## Commercial Production Blockers

### Infrastructure And Release

- Production hosting provider and deployment topology are not selected for
  commercial production.
- Production environments are not codified.
- Coolify-first staging is deployed and smoke-tested on
  `afk4.staging.mubi.dev`; staging API/database/session secrets were rotated
  after the rehearsal. A GitHub Actions workflow now automates ordinary staging
  backend deploys through the Coolify API, with a fail-closed EF migration
  guard.
- Automated mandatory PR checks are not enforced by GitHub rulesets on the
  current private repository plan; manual green-check merge discipline is
  recorded in `AGENTS.md`.
- Migration rehearsal is documented but not automated.
- No production incident/rollback checklist exists for backend deployment.

### Data Protection

- Backup/restore runbook and scripted helper exist, and both local Docker and
  real Coolify staging restore rehearsals have passed.
- Backup encryption, retention, off-host storage, and restore owner must be
  named before launch.
- Point-in-time recovery and provider-managed backup policy are not selected.

### Secrets And Signing

- Production Authenticode certificate authority is undecided.
- Certificate storage policy is undecided.
- ECDSA update metadata signing key storage policy is undecided.
- Earlier Coolify API token and staging database/session secrets used during
  the first rehearsal were rotated. The Coolify API token used during the
  2026-05-19 restore rehearsal was exposed in chat; rotating it remains
  operational hygiene before sensitive staging operations, but it is not a
  pilot or development blocker. Future secret exchange must stay out of chat.
- Staging update artifacts now use Coolify-hosted MinIO. Production
  object-store/CDN provider, public-read policy, retention, and presigned
  upload automation are undecided.
- Update package registration currently supports short-lived staff tokens;
  service credential policy is still open.

### Agent And Windows Runtime

- Automatic Agent-side consumption of rotated device credentials is not
  implemented.
- Agent service registration now has matching Windows Service host lifetime
  wiring and Windows 11 VM smoke evidence. Physical service startup validation
  remains hardening through the real-device smoke runbook.
- A staging-only Gaming PC bootstrap path exists. The older release-workstation
  setup executable path remains in code, but the preferred clean VM path is now
  the MinIO-hosted remote bootstrap script from `Package Smoke`:
  `https://updates.afk4.staging.mubi.dev/afk4-updates-staging/bootstrap/gaming-pc/internal/latest.json`.
  A first Windows 11 VM passed rebuilt x64 install/enroll/heartbeat, session
  start/end, signed lease, local runtime state, and visible Player Shell
  active/locked evidence. A second Windows 11 VM smoke confirmed
  interactive-session Shell auto-start and active-state delivery without manual
  Shell restart after the long-lived state pipe fix. The first remote bootstrap
  run reached enroll/seat assignment but failed MSI install with 1920/1603
  because service startup happened before machine config was written. Corrected
  bootstrap version `0.1.14` passed clean Windows 11 VM install/enroll/
  seat-assignment/heartbeat/locked-Shell smoke, then passed two session
  start/end cycles with no-SQL seat/device reuse. Repeat that remote bootstrap
  path on physical Windows hardware as hardening before wider rollout.
- Lock/unlock enforcement has adapter coverage and Windows 11 VM smoke
  evidence. Physical Windows validation remains hardening before wider rollout.
- Player Shell service-session competition is mitigated in code by
  session-aware process detection and Agent-driven launch into the active
  interactive Windows session. The Agent-to-Shell state pipe now serves the
  latest state to late or restarted Shell clients instead of relying on a short
  publish timing window. A rebuilt Windows 11 staging VM confirmed active-state
  delivery without manual Shell restart; physical PC smoke remains recommended
  hardening.
- Session end/finalization is implemented in code for accepted/completed lock
  command results and heartbeat recovery when accepted lock results were
  already persisted for an `ending` session, including duplicate-result
  idempotency and seat/device reuse tests. After staging was redeployed from
  `codex/staging-gaming-pc-bootstrapper`, Windows 11 VM smoke confirmed an
  ended session returned the seat/device to locked and a second session started
  on the same seat without SQL cleanup. A later remote bootstrap `0.1.14` VM
  smoke also confirmed reuse, but exposed duplicate backend lock command
  creation on the final session end. Issue #36 is now fixed, redeployed, and
  closed: the staging VM recheck on 2026-05-19 confirmed session
  `1df4e315-9585-47af-9c74-02c2ebe423de` produced exactly one fresh lock
  command, then returned the seat/device to locked with no active session.
- Reboot recovery should be exercised on physical PCs as hardening before wider
  rollout.
- Already enrolled PCs are updateable through signed/internal MSI update
  rollouts in staging: the Windows 11 VM device
  `0588fb59-3edb-4704-bbdb-094e12417cf1` installed Agent/Shell `0.1.3` and
  then `0.1.6` and `0.1.7` from MinIO. The `0.1.7` rollout adds stale recovery
  state handling and atomic artifact download behavior for partial downloads,
  sleep, reboot, and network loss. The `0.1.7` VM run exposed a backend bug
  where older active rollouts could still be offered after a newer version was
  installed; PR #22 fixed the offer filter and staging was redeployed. Manual
  copying of rebuilt client packages is no longer the preferred clean-machine
  path; use the remote bootstrap manifest/script for clean staging PCs and the
  signed/internal MSI rollout path for already enrolled PCs. No separate update
  development branch is planned now; repeat update and rollback evidence on
  physical hardware as broader release hardening.
- Production lease duration and heartbeat refresh threshold need telemetry.

### Operator Workflows

- Staging smoke proved the old Operator App layout was not viable even though
  session start/end worked. After reviewing the WPF redesign branch, the
  go-forward Operator App runtime changed to a native .NET Windows desktop
  shell with WebView2 and React/TypeScript UI. This keeps the native Windows
  app boundary and explicitly does not introduce a browser-delivered web admin
  panel. `docs/product/operator-app-ui-target.md` remains the accepted UI/UX
  target: dense floor-map-centered operator console, selected-seat action
  panel, operational signals, explicit pending/failed backend and device
  states, and no raw GUID/form surfaces in normal cashier/operator paths.
  `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`
  is the focused migration plan. The first implementation increment now starts
  a WebView2 host shell and a local React/TypeScript console with host config
  injection, local asset resolution, the floor map, and SmartShell-inspired
  fixture workspaces for dashboard, booking, POS/shop, clients, payments,
  logs, and ops/settings. The first native auth/token boundary now exists:
  staff sign-in, token load/refresh/sign-out bridge messages, protected token
  storage, and React auth gating are test-covered. Typed frontend API client
  boundaries now also exist for floor map, sessions, POS, players,
  shifts/reports, settings/pilot setup, devices, diagnostics, updates, and
  audit. The primary floor map now also loads backend `FloorMapDto` data after
  native staff auth and applies SignalR `deviceStatusChanged` updates from
  `/hubs/devices` as contextual live state. Selected-seat fast guest start,
  extend +15/+30, transfer, and end actions now call backend session APIs,
  wait for confirmation, use idempotency keys, and reload the authoritative
  floor map. POS, Clients, Payments, Logs, and Settings now consume existing
  backend endpoints for their first React parity pass; POS checkout creates a
  backend sale and manual payment before confirming UI success. Dashboard now
  has a backend summary endpoint and React wiring for active shift,
  revenue/utilization, alert pressure, focus queue, recent payments, and export
  fetch confirmation. Booking now consumes reservation API contracts for
  search/create/update/confirm/seat/cancel actions, with floor-map availability
  as a supporting view. Billing-mode selection
  beyond fast guest, permission-aware action state, device command result
  state, reservation edge-case hardening, general profile/staff invitation
  settings, and staging smoke of these extra workspaces still remain before the
  WebView2/React app covers the full pilot day flow. Fixture-only/missing-contract
  commands should report backend failures rather than showing backend success.
  The current WPF implementation remains a parity reference and temporary legacy
  source until the WebView2/React Operator App covers the pilot day flow.
- Staff management workflow is implemented as a minimum API path on `main`;
  the Operator App has a minimum one-shot Pilot Setup panel, but not a general
  staff management UI.
- Custom roles and role editing UI are not implemented.
- Branch layout management is implemented as a minimum API path on `main`;
  the Operator App has a minimum one-zone/seats Pilot Setup panel, but not a
  general layout editor.
- Device-seat assignment has a staff-authorized API path and staging smoke
  setup integration plus optional assignment in the Pilot Setup panel, but no
  general device/seat management UI yet.
- Pilot branch setup can now run through either the Operator App Pilot Setup
  panel or the Platform API script fallback. Commercial production still needs
  broader operator-safe configuration screens.

### Observability And Support

- Backend and Agent logs exist at implementation level, but production log
  aggregation, metrics, alerting, and correlation policy are not configured.
- Diagnostics screen exists, but support runbooks for common incidents are
  still needed.

## Recommended Next Branches

1. Operator App WebView2/React migration

   Continue
   `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`.
   The native WebView2 host, React/TypeScript app foundation, typed config
   bootstrap, native auth/token bridge, protected staff sign-in, typed
   frontend API client boundary, local floor-map concept UI, backend-backed
   primary floor-map loading, SignalR device-status state, and
   backend-confirmed selected-seat start/extend/transfer/end actions now exist.
   POS, clients, payments, logs, and settings now have first-pass backend data
   and action wiring where current backend contracts exist; Booking now also has
   backend reservation contract wiring for search/create/update/confirm/seat/cancel
   flows. Dashboard now has first-pass backend metrics. Next deliver
   permission-aware React state, map action parity gaps such as billing-mode
   selection and device command result status, reservation edge-case hardening,
   and staging smoke across these backend-backed workspaces. Local builds must
   still target staging with
   `AFK4_OPERATOR_PLATFORM_BASE_URL=https://afk4.staging.mubi.dev`. Treat raw
   GUID/form surfaces in the main operator path as usability defects unless
   they are explicitly advanced technician tools.

2. Operator-facing management expansion

   The one-shot Pilot Setup panel is enough for pilot setup. Next development
   should expand toward general staff/role, layout, device-seat, tariff, POS,
   and runtime/staging configuration screens as pilot usability requires.

3. Physical Windows hardening

   Repeat `docs/operations/real-device-windows-pc-smoke.md` on physical
   Windows 10/11 hardware when hardware is available. Treat findings as
   hardening work unless they block the current Operator App staging test.

4. Staging secret hygiene

   Rotate the exposed restore-rehearsal Coolify token before sensitive staging
   operations, then keep future tokens in a secret manager or local
   runtime-only environment, not in chat or repository files. GitHub repository
   variables `COOLIFY_BASE_URL` and `COOLIFY_STAGING_APP_UUID`, plus secret
   `COOLIFY_API_TOKEN`, are configured for automated staging deploy.

## Decision Rules

- Do not add web admin, local server, non-Windows agents, microservices, kernel
  driver, fiscal integrations, or mobile app to solve production readiness
  unless the PRD and architecture spec are updated first.
- Prefer runbooks and explicit release gates before adding provider-specific
  SDKs.
- Prefer one real-device smoke loop over more theoretical docs once staging is
  available, but do not treat physical hardware availability as a blocker for
  current Operator App staging tests.
- Treat backup restore as a launch gate. Treat physical-device release
  validation as hardening and release-confidence work unless a concrete
  Operator App staging test is blocked by it.
