# AFK4 Production Readiness Roadmap

Last updated: 2026-06-10

## Purpose

This roadmap tracks what separates the current AFK4 codebase from production.
It is intentionally operational: infrastructure, release gates, security,
backups, device validation, and pilot readiness. Keep it gate-level; detailed
run history belongs in the progress snapshot or archive notes.

> **Status note (2026-06-10):** the product/feature side has moved far beyond
> the pilot bar — the full Platform.Web redesign, SP3 admin control plane +
> SaaS billing, the entire SP4 wave (counter-loop, anti-fraud, offline,
> customer portal/shell, notifications, localization, realtime), the dcgate
> payments subsystem, the Operator App WebView2/React migration, the
> phone/email staff-identity wave, and the FE forgot/reset-password screen are
> all implemented and merged. Since 2026-06-04 the customer-shell WebView2
> pivot shipped end to end — login, self-service extend/top-up, plus Unit F
> (shop, loyalty/cashback, news/banners) — together with multi-tenant dcgate
> payments (now on a shared AFK4 Telegram app: owners supply phone + OTP only),
> shift-revenue reporting, branch-timezone-aware business-day rollup, and real
> PNG PWA icons. What remains for production is mostly the operational gates
> below (backups ownership, Authenticode/signing custody, production
> object-store/CDN, physical Windows smoke, secret hygiene) plus, for the
> payments money-path, an external blocker: the DC bank bot stopped delivering
> deposit notifications, so the real payment → webhook → wallet-credit step is
> frozen until the bot recovers (attach + session-sharing are proven live on
> staging). The customer-shell still needs one on-device (real gaming PC)
> kiosk/login/QR/offline smoke gate. See
> `docs/progress/2026-05-12-vertical-slice-progress.md` for the current
> feature snapshot.

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
- One tenant and first branch can be provisioned without direct database edits
  through a scripted or Control Plane path.
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
- Internal SaaS Control Plane exists for platform-owner tenant onboarding,
  subscription/status controls, owner invites, tenant health, support notes,
  and suspend/reactivate.
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

2. **Tenant Onboarding And SaaS Control Plane**

   The product decision changed on 2026-05-23: AFK4 MVP now includes an
   internal browser-based SaaS Control Plane for the platform owner/support
   role. The first pilot can still use a scripted provisioning fallback, but
   commercial production must not require direct PostgreSQL edits to create
   organizations, branches, owner invites, plan/status metadata, tenant limits,
   support notes, or suspend/reactivate state.

3. **CI Gate**

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
   green remote `PR Verification Result` before merge. A local 2026-05-26
   readiness pass found that the manual `Client Packages` workflow was rejected
   by GitHub before job creation because `workflow_dispatch` had 27 top-level
   inputs. The pushed fix keeps it under GitHub's 25-input limit by replacing
   the six HTTP PUT artifact URI inputs with one JSON input; `actionlint` and
   focused release-automation tests passed locally. Later `Package Smoke` runs
   are green, but a successful remote manual `Client Packages` dispatch is not
   yet recorded.

4. **Windows Endpoint Smoke**

   Enroll a Windows 10/11 test endpoint. Validate device credential auth,
   heartbeat, SignalR commands, session start/end, lease refresh, lock/unlock,
   Player Shell state, installed app report, and diagnostics. A repeatable
   manual staging runbook for physical hardware still exists at
   `docs/operations/real-device-windows-pc-smoke.md`, and the Agent host is
   wired for Windows Service runtime under service name `AFK4.Agent.Service`.
   The current clean-machine path is the single `AFK4 Agent` MSI plus owner-code
   Setup Wizard. The older one-click Gaming PC setup executable and coordinated
   `afk4-gaming-pc` MSI have since been retired and removed from the codebase. Windows 11 VM
   smoke reached internal Agent `0.1.29`: VM2 applied the rollout, rebooted,
   kept `AFK4.Agent.Service` running, and did not reopen Setup Wizard. This is
   enough to proceed with pilot Operator App testing and continued development.
   A 2026-05-26 `manager_workstation` smoke found and fixed the local
   seat-assignment model bug: manager workstations must enroll without a
   floor-map seat. The nullable manager-workstation install-enrollment backend
   deployed to staging on 2026-05-27 from `cac13da`; Package Smoke run
   `26507696459` published internal Operator App `0.1.33` and created the
   staging branch rollout. A repeatable API cleanup helper now exists at
   `scripts/cleanup-manager-workstation-smoke-data.ps1`; staging still needs
   that helper run with the smoke staff credential, followed by a repeat
   clean-VM manager-workstation run before that evidence can be closed.
   Physical Windows 10/11 hardware, reboot recovery repeats, and
   update/rollback repeats remain hardening work before wider operational
   rollout.

5. **Backup And Restore Rehearsal**

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

6. **Signed Client Release Rehearsal**

   Staging now has a temporary pilot update-hosting path using Coolify-hosted
   MinIO at `updates.afk4.staging.mubi.dev`. The package smoke workflow can
   build MSI artifacts, publish signed update metadata to staging MinIO,
   register packages with the staging Platform API, and create an internal
   device rollout. Client package workflows now set up Node 24 and the package
   script builds `src/AFK4.Operator.App.Web`, then copies the fresh Vite `dist`
   output into the Operator App publish `WebAssets` before WiX builds the
   Operator App MSI; the script also asserts the finished MSI contains the
   frontend `index.html`, JavaScript, and CSS files. The Operator App MSI now
   also has a WebView2 Evergreen Runtime launch condition using the documented
   EdgeUpdate `pv` registry values, so unsupported machines fail closed with a
   clear prerequisite message. Slice 3.2 adds a new WiX-built
   `afk4-agent-<version>-<channel>.msi` onboarding artifact that contains the
   Agent Service, WPF Setup Wizard, update helpers, Start Menu shortcut,
   first-run marker, HKLM `RunOnce`, and an interactive postinstall wizard
   launch attempt. The service is registered for automatic startup but is not
   started by the MSI before owner-code enrollment writes machine
   configuration; the wizard starts it after successful enrollment. Slice 3.3
   adds role-aware Agent component installation:
   `gaming_pc` devices pull a standalone Player Shell MSI, `manager_workstation`
   devices pull the Operator App MSI after a WebView2 runtime check/install,
   and update metadata publishing now uses separate Operator App, Agent, and
   Player Shell MSI artifacts. The corrected single Agent MSI path now has
   Windows 11 VM evidence through internal Agent version `0.1.29`; the legacy
   coordinated `afk4-gaming-pc` MSI and `AFK4.GamingPc.Setup` bootstrapper have
   since been removed from the codebase entirely (they were already gone from the
   default package-smoke and publishing flow). The
   package-smoke follow-up now creates a staging branch rollout for
   `operator-app` while keeping the Agent Service rollout device-targeted, so
   clean manager-workstation smoke no longer depends on manually installing the
   Operator App MSI once that workflow has run on `main`; the registration
   script also handles branch rollouts without device targets.
   Commercial production still needs final Authenticode/signing custody,
   production storage/CDN policy, and service credentials for package
   registration. Physical PC update/rollback evidence remains broader release
   hardening.

7. **Pilot Setup Runbook**

   Document exactly how to create the first organization, branch, staff users,
   roles, zones, seats, devices, tariffs, POS products, and update channels for
   a pilot club. Staff-authorized setup APIs, the pilot setup script, and the
   minimum Operator App setup panel exist. Club self-service onboarding now has
   backend install APIs, branch device admin APIs, owner-facing Platform.Web
   screens on `main` and staging, separate admin/customer SPA builds, and a
   deployed customer staging app. The single `AFK4 Agent` MSI includes Setup
   Wizard, role-aware Player Shell / Operator App component install, and
   Windows 11 VM reboot/service-start evidence. The legacy coordinated
   gaming-PC MSI path is retired from the default onboarding/package flow and
   remains a fallback. Remaining pilot setup evidence is the repeat
   `manager_workstation` clean-VM smoke after cleanup of mistaken smoke seats
   and assignments.

## Commercial Production Blockers

### Infrastructure And Release

- Production hosting provider and deployment topology are not selected for
  commercial production.
- Production environments are not codified.
- Internal SaaS Control Plane and no-DB-edit tenant provisioning are partially
  implemented and smoke-tested in staging for platform-admin tenant creation,
  owner invites, tenant status, support notes, and health. The customer SPA
  host is deployed, and the Windows SetupWizard/single Agent MSI path has
  Windows 11 VM evidence through Agent `0.1.29`. The legacy bootstrap/
  coordinated gaming-PC MSI path is retired from the default publishing flow;
  remaining onboarding evidence is any strict `manager_workstation` role smoke
  not yet captured.
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
  the first rehearsal were rotated. Coolify API tokens used during the
  2026-05-19 restore rehearsal and 2026-05-25 staging workflow tail fix were
  exposed in chat; rotating/revoking them remains operational hygiene before
  sensitive staging operations, but it is not a pilot or development blocker.
  Future secret exchange must stay out of chat.
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
- The preferred onboarding path is now the single `afk4-agent` MSI with the
  owner-code Setup Wizard plus role-aware Player Shell/Operator App
  installation. The older release-workstation setup executable and coordinated
  gaming-PC MSI remain in code behind explicit fallback switches only. The
  single Agent MSI path reached internal Agent `0.1.29` on VM2 with update,
  automatic service-start after reboot, and no Setup Wizard rerun after
  upgrade. Repeat the current Agent MSI path on physical Windows hardware as
  hardening before wider rollout.
- Lock/unlock enforcement has adapter coverage and Windows 11 VM smoke
  evidence. Physical Windows validation remains hardening before wider rollout.
- Player Shell service-session competition is mitigated in code by
  session-aware process detection and Agent-driven launch into the active
  interactive Windows session. The Agent-to-Shell state pipe now serves the
  latest state to late or restarted Shell clients instead of relying on a short
  publish timing window. A rebuilt Windows 11 staging VM confirmed active-state
  delivery without manual Shell restart; physical PC smoke remains recommended
  hardening.
- Session end/finalization is implemented for accepted/completed lock command
  results and heartbeat recovery when accepted lock results were already
  persisted for an `ending` session, including duplicate-result idempotency and
  seat/device reuse tests. Issue #36 duplicate lock planning is fixed and
  staging-rechecked.
- Reboot recovery should be exercised on physical PCs as hardening before wider
  rollout.
- Already enrolled PCs are updateable through signed/internal MSI update
  rollouts in staging. VM2 applied the `0.1.29` Agent rollout and survived the
  final reboot with `AFK4.Agent.Service` running. Manual copying of rebuilt
  client packages is no longer the preferred clean-machine path; use the
  current single Agent MSI for clean staging PCs and the signed/internal MSI
  rollout path for already enrolled PCs. No separate update development branch
  is planned now; repeat update and rollback evidence on physical hardware as
  broader release hardening.
- Production lease duration and heartbeat refresh threshold need telemetry.

### Operator Workflows

- Staging smoke proved the old Operator App layout was not viable even though
  session start/end worked. After reviewing the WPF redesign branch, the
  go-forward Operator App runtime changed to a native .NET Windows desktop
  shell with WebView2 and React/TypeScript UI. This keeps the native Windows
  app boundary and keeps day-to-day club operations out of the internal SaaS
  Control Plane. `docs/product/operator-app-ui-target.md` remains the accepted
  UI/UX
  target: dense floor-map-centered operator console, selected-seat action
  panel, operational signals, explicit pending/failed backend and device
  states, and no raw GUID/form surfaces in normal cashier/operator paths.
  `docs/archive/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`
  is the focused migration plan (now complete; archived). The first implementation increment now starts
  a WebView2 host shell and a local React/TypeScript console with host config
  injection, local asset resolution, the floor map, and SmartShell-inspired
  fixture workspaces for dashboard, booking, POS/shop, clients, payments,
  logs, and ops/settings. The native host can be pointed at staging with
  platform URL, organization id, branch id, and currency environment variables.
  The first native auth/token boundary now exists:
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
  CSV download. Booking now consumes reservation API contracts for
  search/create/update/confirm/seat/cancel actions, with floor-map availability
  as a supporting view. React now also has first-pass permission-aware state:
  the workspace rail and selected-seat session actions disable themselves when
  the restored staff session lacks the required backend permissions. Billing-mode
  selection beyond fast guest and selected-seat device command result feedback
  now have a first React implementation for the map panel, and the primary map
  now has real problem/offline/free/active filters plus table view parity.
  Map `Техрежим` now requires diagnostics/device-detail permissions and reads
  the selected device detail plus branch diagnostics before confirming.
  Booking now also disables mutation controls without `reservations.manage` or
  when selected reservation state/seat availability makes the action invalid.
  Settings now supports general staff creation, login/display-name editing, and
  predefined branch-role reassignment through staff APIs, branch profile
  name/city save through a new branch profile API,
  POS category/product creation plus product update/deactivation through the
  existing POS catalog endpoints, and inventory stock movement creation through
  the existing stock-movement endpoint. The same Settings section now reads recent stock movement history
  through `GET /api/branches/{branchId}/inventory/stock-movements` with
  `inventory.view` gating, plus tariff/version creation/update/deactivation
  and package definition creation/update/deactivation through the existing
  tariff/package endpoints. Settings `Интеграции` now also
  registers update packages, creates update rollouts, shows selected rollout
  status/device snapshots, and changes package/rollout states through the
  existing update endpoints. Logs now applies backend audit search filters for exact
  action, outcome, target type, UTC date range, and limit, and selected Logs
  event detail now uses the already loaded audit/diagnostics backend rows.
  Logs source cards now filter the loaded event list by all/Agent/POS/
  Operator/Platform, and operator period presets execute audit searches for
  today, the last 24 hours, or the last 7 days. Logs export buttons now
  download backend operator-action/shift CSV files and local audit/error JSON
  bundles from loaded audit/diagnostics data.
  Settings `Залы и ПК` now also
  creates device enrollment codes, assigns enrolled devices to seats, and
  opens device detail through existing device endpoints with status/version/
  credential/app counts plus selected-device and branch-wide command history,
  including credential rotation/revocation controls. The same Settings section now creates layout
  zones and seats from operator-entered names/sort orders, updates selected
  zone/seat names, sort orders, and seat zones, and safely deletes unused seats
  plus empty zones through layout endpoints with `layout.manage` gating.
  Payments now opens shifts through the existing open-shift API, closes the
  current shift through the existing close-shift API with counted cash and a
  closing note, and records cash movements through the existing shift cash
  movement API. Payments selected operation detail now uses already loaded
  backend sales/cash report rows for id, shift, source, and line/reason
  context, and Payments report export buttons now download backend sales/cash/
  shift CSV files plus a local discrepancy JSON. POS quick refund now calls the
  existing
  refund endpoint for the selected backend sale, and POS draft void creates a
  backend draft from the current cart before calling the existing void endpoint.
  POS recent receipt rows now open backend sale details through the existing
  sale lookup endpoint and then read the linked receipt projection through the
  existing receipt endpoint for receipt number/type/total display; the loaded
  receipt can now be printed or exported locally from the POS detail panel. POS
  cart customer lookup now searches backend players and attaches the selected
  nullable `playerAccountId` to checkout and draft sale creation through the
  shared POS sale contract and EF-backed Platform API persistence. POS cart
  new-customer creation now posts to the existing branch player API and selects
  the created player for checkout. POS quick deposit top-up now posts the
  selected cart client's current cart total to the existing wallet top-up
  endpoint with `billing.wallet.top_up` gating. POS quick stock write-off now
  records an inventory stock movement through the existing stock-movement
  endpoint with `inventory.stock.manage` gating. Clients profile now reads
  active player packages through the existing player packages endpoint. Clients
  package purchase now calls the existing package option and purchase endpoints
  for the selected backend player with an explicit package selector, price/
  minute preview, deposit guard, and active-package refresh after confirmation,
  and Clients wallet top-up sends
  operator-entered amount/reason to the existing top-up endpoint. Clients debt
  payment sends operator-entered amount/reason to the
  existing debt payment endpoint. Clients player creation sends
  operator-entered name/phone to the existing player creation endpoint. Clients
  reservation creation from the selected backend player now stays disabled
  unless the restored staff session includes `reservations.manage`. Settings
  device setup now reads branch device inventory through
  `/api/branches/{branchId}/devices`, lets operators select a device without
  typing a GUID, and then opens the existing device detail/command/credential
  tools plus selected-device command history from
  `/api/devices/{deviceId}/commands`. A focused pilot-hardening plan now exists
  at `docs/archive/superpowers/plans/2026-05-23-operator-app-pilot-hardening.md`. Its
  first slice removes signed-in POS/Clients fixture leakage for authoritative
  empty backend responses and restricts WebView2 dev-server URLs to loopback.
  Staging smoke of these extra workspaces still remains before
  the WebView2/React app covers the full pilot day flow. Fixture-only/
  missing-contract commands should report backend failures rather than showing
  backend success, and signed-in workspace status copy now reserves fixture data
  for explicit `Dev demo` browser-dev/no-backend fallback states instead of
  normal operator-facing labels. Packaged `webview2` auth failures also project
  host-bridge availability problems into operator-facing restart/check-host copy
  while browser-dev keeps the raw bridge diagnostic for local smoke. Payments
  and Logs now distinguish successful empty backend responses from loading,
  failed, and search/filter-miss states instead of using generic
  `Нет backend ...` placeholders.
  The current WPF implementation remains a parity reference and temporary legacy
  source until the WebView2/React Operator App covers the pilot day flow.
- Staff management now has general Operator App creation, login/display-name
  editing, predefined branch-role reassignment, activation/deactivation, and
  password reset paths.
- Custom roles and arbitrary permission-set editing are not implemented.
- Branch layout management is implemented as a minimum API path on `main`;
  the Operator App now has Settings creation/update controls for zone and seat
  names, sort orders, seat moves, unused-seat deletion, and empty-zone deletion,
  but not visual drag/drop layout editing or soft archive flows.
- Device-seat assignment has a staff-authorized API path and staging smoke
  setup integration plus Settings inventory/assignment/detail/command/
  command-history/credential controls and a branch-wide command-history
  browser. Broader non-command device telemetry/event browsing is still
  missing.
- Pilot branch setup can now run through either the Operator App Pilot Setup
  panel or the Platform API script fallback. Commercial production still needs
  the internal SaaS Control Plane for platform-owner tenant onboarding/support
  plus broader operator-safe configuration screens inside the native Operator
  App.

### Observability And Support

- Backend and Agent logs exist at implementation level, but production log
  aggregation, metrics, alerting, and correlation policy are not configured.
- Diagnostics screen exists, but support runbooks for common incidents are
  still needed.

## Recommended Next Work

The Operator App WebView2/React migration, the SaaS Control Plane, and the
full SP3/SP4 feature wave are done and merged (see the status note at the top
and the progress snapshot). The remaining path to production is operational:

1. **Staging smoke** — run the deferred staging smoke suite when the owner
   gives the go: the `manager_workstation` clean-VM repeat (after running
   `scripts/cleanup-manager-workstation-smoke-data.ps1 -Apply
   -DeleteEmptySmokeSeats` with the smoke staff credential) and a full
   backend-backed operator day-flow pass. The Coolify staging app deploys
   `main`, so anything smoke-tested must already be on `main`.

2. **Physical Windows hardening** — repeat
   `docs/operations/real-device-windows-pc-smoke.md` on physical Windows 10/11
   hardware: lock/unlock enforcement, reboot recovery, role-aware updates, and
   update/rollback. Treat findings as hardening unless they block a concrete
   staging test.

3. **Pre-production release decisions** — settle the commercial blockers below:
   Authenticode/signing custody, production object-store/CDN provider + retention
   + presigned upload automation, package-registration service credentials,
   backup encryption/retention/restore ownership, and a production
   incident/rollback checklist.

4. **Secret hygiene** — keep Coolify/API tokens in a secret manager or
   runtime-only env, never in chat or repo files. GitHub variables
   `COOLIFY_BASE_URL` / `COOLIFY_STAGING_APP_UUID` and secret
   `COOLIFY_API_TOKEN` are configured for automated staging deploy.

5. **Finish password reset** — the backend reset path + MailKit SMTP transport
   exist; build the FE forgot/reset-password screen (currently a placeholder)
   and wire per-environment SMTP config.

## Decision Rules

- Do not add local server, non-Windows agents, microservices, kernel driver,
  fiscal integrations, mobile app, or customer browser operational admin as the
  primary club UI to solve production readiness unless the PRD and architecture
  spec are updated first.
- Prefer runbooks and explicit release gates before adding provider-specific
  SDKs.
- Prefer one real-device smoke loop over more theoretical docs once staging is
  available, but do not treat physical hardware availability as a blocker for
  current Operator App staging tests.
- Treat backup restore as a launch gate. Treat physical-device release
  validation as hardening and release-confidence work unless a concrete
  Operator App staging test is blocked by it.
