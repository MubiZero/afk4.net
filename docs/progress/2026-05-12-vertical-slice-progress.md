# AFK4 Current Progress Snapshot

Status: the first MVP-oriented vertical slice is implemented through client
packaging, signed update metadata registration automation, diagnostics, reports,
audit search, and backup/restore runbooks.

Last updated: 2026-05-21

## Purpose

This file is the compact current-state snapshot for new sessions. It replaces
the old long-form progress log, which is archived at:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`

Use the archive only when historical verification details or phase-by-phase
implementation evidence are needed.

## Implemented Capabilities

### Backend Platform

- ASP.NET Core modular monolith foundation on .NET 10.
- EF Core/Npgsql persistence and migrations for identity, tenancy, devices,
  layout, sessions, billing, POS, shifts, updates, audit, diagnostics, and
  reports.
- Reservations module with branch-scoped reservation search/create/update,
  confirm/seat/cancel actions, overlap checks, and reservation audit entries.
- Staff sign-in and refresh-token rotation.
- Predefined MVP role-to-permission mapping.
- Branch-scoped authorization for implemented operator-facing endpoints.
- Device enrollment, credential issuance, heartbeat validation, command
  dispatch/status, command result fallback, credential rotation, and revocation.
- Persisted zones, seats, staff-authorized device-seat assignment, floor-map
  reads, installed app reporting, and device detail projections.
- Session start, extend, transfer, end, signed leases, reconciliation, and
  heartbeat-driven lock/unlock/lease-refresh command planning.
- Immutable ledger-backed wallet, debt, packages, refunds, manual corrections,
  tariffs, package definitions, and package consumption.
- POS catalog, stock movements, sales, manual payments, refunds, voids,
  receipts, shifts, cash movements, and shift close reconciliation.
- Update package registration, package/rollout state changes, rollout status
  reads, device update check/status endpoints, and Agent update status tracking.
- Audit search endpoint.
- Branch diagnostics endpoint.
- Operational reports and CSV exports for shifts, sales, gameplay time, cash
  operations, and operator actions.
- Operator Dashboard summary endpoint for active shift, revenue/utilization,
  alert pressure, focus queue, floor-map-derived availability, and recent
  payments.

### Operator App

- The Operator App target runtime is now a native .NET Windows desktop shell
  with WebView2 hosting a React/TypeScript operator UI. The existing WPF/MVVM
  implementation is legacy migration source, not the go-forward UI runtime.
- Protected token storage abstraction.
- Permission-filtered navigation.
- Realtime floor map loading and selected-seat session actions.
- Player search, wallet/package summaries, POS, shifts, reports, CSV exports,
  settings, technician device tools, update package/rollout management, audit
  search, diagnostics, and production hotkeys.
- A first operator-console redesign pass is in progress on
  `codex/operator-app-redesign`: the shell, floor map, POS, players, shifts,
  and settings surfaces now prioritize selected-seat context, readable state,
  cart/checkout clarity, and operator-safe summaries instead of raw GUIDs and
  backend-shaped forms.
- Settings includes a minimum Pilot Setup panel for branch staff users, one
  layout zone with seats, one tariff/version, one POS category/product, and
  optional already-enrolled device-to-seat assignment.
- Local Operator App builds can target staging by setting
  `AFK4_OPERATOR_PLATFORM_BASE_URL` before launch.
- Floor-map seat context no longer defaults to a raw `postpaid_debt` request.
  It has a fast guest/no-ledger billing option for staging smoke, explicit
  billed modes, and validation that billed modes require a player account.
- The Operator App redesign branch now makes the primary operator day flow
  Russian-first for sign-in, floor map/seat actions, POS, players, and shifts.
  Operator money commands use configurable UI currency from
  `AFK4_OPERATOR_CURRENCY_CODE`, defaulting to `TJS`.
- The target state for continued Operator App UI/UX work is now documented in
  `docs/product/operator-app-ui-target.md`. Future redesign work should
  converge to that dense operator-console target: dark top/left chrome, floor
  map as the main workspace, selected-seat action panel, operational signals,
  explicit pending/failed backend and device states, Russian-first primary copy,
  and no raw GUID/form surfaces in normal cashier/operator paths.
- The approved migration plan is
  `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`.
  It keeps the native Windows desktop app boundary and MSI/update component,
  but moves operator UI implementation from WPF to WebView2 + React/TypeScript.
- The WebView2/React Operator App now has the first real auth/token boundary:
  the native host handles `auth:loadToken`, `auth:signIn`, `auth:refresh`, and
  `auth:signOut` bridge messages; tokens are saved through Windows protected
  storage; the React UI gates the operator console behind staff sign-in; and
  auth/error-projection tests cover that browser `localStorage` and
  `sessionStorage` are not used for token persistence.
- The React frontend now also has typed API client boundaries beyond auth:
  shared authenticated HTTP/error handling plus route-tested clients for floor
  map, sessions, POS, players, shifts/reports/CSV, settings/pilot setup,
  devices, diagnostics, updates, and audit. The primary map now uses those
  clients to load backend floor-map data after native staff auth, while keeping
  fixture fallback for browser-dev/no-backend runs.
- The React primary floor map now has a SignalR JavaScript client for the
  existing `/hubs/devices` hub. It tracks disconnected/connecting/connected/
  reconnecting state, applies `deviceStatusChanged` updates by device id or
  machine name, preserves active sessions as warning/problem seats when the PC
  goes offline, and treats realtime as context only.
- The React primary floor map now sends selected-seat fast guest start,
  extend +15/+30, transfer, and end requests through typed authenticated
  session API clients. Those actions are enabled only for backend-loaded seats
  with the required session/seat ids, use idempotency keys, wait for backend
  confirmation, reload the authoritative floor map, and show failed backend
  responses as action feedback instead of confirming fixture-only clicks.
- The React UI now has its first permission-aware state: the workspace rail
  disables screens when the restored staff session lacks the backend
  permissions those screens need, and selected-seat start/extend/transfer/end
  actions are disabled and guarded by matching session permissions before any
  API call is attempted.
- The selected-seat map panel now supports real billing-mode selection for
  guest/no-ledger, prepaid wallet, player package, and postpaid debt starts and
  extensions. Billed modes require a backend player selection plus the relevant
  tariff or active player package before the session command is enabled, and
  session command feedback now reads backend device-command status when the
  staff session has `devices.commands.status.view`.
- The primary map now has real operator filters for all seats, free seats,
  active sessions, attention states, and offline seats, plus a table view that
  uses the same backend-loaded `SeatSummary` state as the map tiles. Changing a
  filter also keeps the selected-seat context on a visible seat.
- Booking now has first-pass edge hardening for the React operator flow:
  create/confirm/seat/move/cancel actions require `reservations.manage`,
  mutation buttons disable for view-only staff or terminal reservation states,
  the selected reservation index is reset after backend reloads when needed,
  and new reservations validate free-seat availability plus start time before
  sending the backend request.
- Settings `Персонал` now has a general staff creation form backed by the
  existing branch staff API. It validates the form client-side, requires the
  existing branch-staff management permission, posts login/display name/
  temporary password/role names to `/api/branches/{branchId}/staff`, and keeps
  branch profile saving as an explicit failed state until a profile endpoint
  exists.
- The remaining React operator workspaces are now wired to existing backend
  reads/actions where contracts exist: POS loads catalog/current shift/sales
  reports and creates paid manual-provider sales; Clients searches backend
  players and performs wallet top-up/debt payment/player creation; Payments
  reads shift, sales, cash, and CSV report endpoints; Logs reads audit and
  diagnostics; Settings reads staff, layout, catalog, diagnostics, update
  rollout, and tariff option data and can trigger limited backend setup
  actions; Dashboard reads the backend dashboard summary and uses existing
  report exports for export confirmation; Booking uses backend reservation
  search/create/update/confirm/seat/cancel endpoints with floor-map-backed
  availability and action fallback. Missing profile/staff-invite contracts
  still fail explicitly instead of showing fixture success.

### Agent Service

- Heartbeat payloads and worker loop.
- Device credential authentication.
- Realtime device hub client.
- Signed session lease validation and persistent lease/runtime state.
- Reconnect reconciliation snapshots.
- Lock/unlock enforcement coordinator through testable adapter boundaries.
- Agent host is wired for Windows Service lifetime so the WiX-registered
  `AFK4.Agent.Service` service can run under the Windows Service Control
  Manager during real-device smoke.
- Player Shell process supervision.
- Named-pipe Shell state publishing and Shell launcher command handling.
- Allow/deny process policy foundation.
- Update check/status client and background update execution worker with
  artifact download, SHA-256 verification, ECDSA metadata signature validation,
  persisted recovery state, install/rollback/restart adapter boundaries, and
  status progression.

### Player Shell

- Fullscreen WPF MVVM UI for locked, active-session, warning, grace/offline,
  ending, and launcher states.
- Receives Agent-published state through local named pipes.
- Sends launcher requests back to the Agent for validation.

### Packaging And Updates

- WiX/MSI baseline:
  - Operator App MSI.
  - Coordinated gaming-PC MSI for Agent Service + Player Shell.
- Local package build script:
  - `scripts/build-client-packages.ps1`
- Provider-neutral Authenticode signing script:
  - `scripts/sign-client-packages.ps1`
- MSI update metadata publishing script:
  - `scripts/publish-client-msi-updates.ps1`
- Staging clean-machine Gaming PC remote bootstrap publishing:
  - `scripts/publish-staging-bootstrapper.ps1`
  - `Package Smoke` publishes a small `install-afk4-gaming-pc.ps1` script and
    `latest.json` manifest to staging MinIO. The script downloads the internal
    Gaming PC MSI from MinIO, verifies SHA-256, enrolls the device through the
    staging API, assigns the smoke seat, installs the MSI, writes Agent machine
    configuration, starts the service, and waits for heartbeat evidence.
- Backend registration script for generated update package request JSON:
  - `scripts/register-update-package-requests.ps1`
- Manual GitHub Actions workflow for build/test/package, optional signing,
  optional metadata publishing, and guarded package registration.
- Cost-aware GitHub Actions workflows:
  - PR verification with branch-protection-safe result job and conditional
    Windows build/test execution.
  - Package smoke for unsigned MSI validation on `main` and manual dispatch.
  - Coolify staging deploy for Platform API changes on `main`, with Coolify API
    queue/polling, `/api/health` verification, and EF migration fail-closed
    guard.
  - Manual release package workflow with short artifact retention.
  - JavaScript actions are opted into Node 24 execution with
    `FORCE_JAVASCRIPT_ACTIONS_TO_NODE24`.

### Operations Docs

- Local PostgreSQL smoke runbook.
- Coolify staging deploy runbook for building the Platform API container from
  the repo, connecting Coolify-managed PostgreSQL, applying EF migrations,
  configuring GitHub repository variables/secrets for automated Coolify deploy,
  and running health/smoke checks.
- Agent installer enrollment runbook.
- Client update rollout runbook.
- Real Windows gaming PC smoke runbook for staging Platform API, Agent Service,
  Player Shell, sessions, leases, lock/unlock evidence, installed apps,
  diagnostics, and update check/status boundaries.
- Client packaging runbook.
- Update package publishing runbook.
- PostgreSQL backup/restore rehearsal runbook with
  `scripts/rehearse-postgres-restore.ps1` for repeatable backup, restore,
  migration update, and table-count sampling from secret-provided PostgreSQL
  URLs.
- Production readiness roadmap.

## Latest Verification

Current local verification on 2026-05-21 from `D:\projects\afk4.net` after
the WebView2/React auth/token, typed API client, SignalR realtime,
backend-backed floor-map loading, backend-confirmed selected-seat actions,
first backend-backed parity wiring for the remaining operator workspaces,
the Operator Dashboard summary endpoint, Booking reservation contracts, and
permission-aware React navigation/session action state, map billing-mode
selection and device-command result feedback, map filters/table parity,
Booking permission/state hardening, and Settings staff creation:

```powershell
& 'C:\Program Files\nodejs\npm.cmd' test
& 'C:\Program Files\nodejs\npm.cmd' run build
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\Git\cmd\git.exe' diff --check
```

Result:

- frontend tests: 42 passed, 0 failed;
- frontend production build: passed;
- full solution tests: 773 passed, 0 failed;
- `git diff --check`: clean apart from expected CRLF conversion warnings;
- Operator Dashboard backend wiring tests cover shared DTO serialization,
  unauthorized/forbidden/success API behavior, denied/succeeded audit records,
  frontend route construction, backend-loaded Dashboard KPIs/focus queue, and
  export feedback that waits for backend reads.
- Booking reservation tests cover shared DTO serialization, unauthorized and
  forbidden reads, create/confirm/update/search/seat/cancel API behavior,
  overlap conflict handling, audit records, frontend route construction,
  Booking create/cancel confirmation, and reservation creation from a backend
  player card.
- Permission-aware frontend tests cover disabled workspace navigation and
  disabled selected-seat actions when the staff session only has
  `floor_map.view`.
- Map billing/action tests now cover fast guest start, prepaid-wallet start
  with backend player/tariff metadata, idempotency keys, and follow-up
  `/api/devices/{deviceId}/commands/{commandId}/status` reads for selected-seat
  command feedback.
- Map filter/table tests cover the free-seat filter, selected-seat handoff when
  the active filter hides the previous PC, and the table view's operator
  columns.
- Booking frontend tests now cover disabled mutation controls for a staff
  session with `reservations.view` but without `reservations.manage`.
- Settings frontend tests now cover branch staff creation from the Personnel
  form, including request body serialization for login, display name, temporary
  password, and role names.
- Browser smoke on `http://127.0.0.1:5173/`: the React app rendered the
  WebView auth entry surface with title `AFK4 Operator`, detected AFK4/sign-in
  copy, and reported no browser console errors.
- Previous browser smoke on `http://127.0.0.1:4174/`: WebView auth entry screen
  rendered with title `AFK4 Operator`, heading `Вход оператора`, password
  field, sign-in button, custom window controls, platform URL, no console
  errors, and no horizontal or vertical page overflow. Because the smoke runs
  outside WebView2, the page correctly reported the native host bridge as
  unavailable.
- SignalR/floor-map frontend tests cover the `/hubs/devices` URL, realtime
  connection-state transitions, backend `FloorMapDto` sorting/state mapping,
  and `deviceStatusChanged` overlays for free and active-session seats.
- Selected-seat action frontend tests cover backend-confirmed session end and
  fast guest start calls, including route selection, request body serialization,
  idempotency keys, and UI confirmation only after the API resolves.
- POS frontend tests now cover backend-confirmed catalog/current-shift loading,
  POS sale creation, manual payment, and UI confirmation only after both API
  calls resolve.
- Booking is now backed by real reservation API contracts and is no longer blocked
  by missing booking contract support. The app now exercises reservation
  search/create/update/confirm/seat/cancel endpoints. Remaining work is UX
  polish for edge cases and staging smoke, not absence of API.
- Typed frontend API client boundary verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  ```

  Result: frontend tests passed 25/25 and Vite production build passed. New
  route tests cover authenticated headers, JSON body serialization, optional
  404/204 handling, CSV text reads, API error projection, and operator clients
  for floor map, sessions, POS, players, shifts/reports, settings/pilot setup,
  devices, diagnostics, updates, and audit.

Earlier final verification after merging PR #9 on 2026-05-16 from
`D:\afk4.net`:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Result:

- 624 passed
- 0 failed
- 0 skipped

Additional verification from the Authenticode CI registration slice:

- targeted client release automation tests passed 25/25;
- PowerShell parser checks passed for signing, publishing, and registration
  scripts;
- full solution build succeeded with 0 warnings and 0 errors;
- package smoke produced both MSI artifacts:
  - `afk4-gaming-pc-0.1.0-ci-internal.msi`
  - `afk4-operator-app-0.1.0-ci-internal.msi`
- `git diff --check` was clean before merge.

CI checks on PR #9 were not configured, so the recorded confidence is local
verification plus PR review, not enforced repository branch protection.

Cost-aware CI configuration verification on 2026-05-17:

- targeted client release automation tests passed locally;
- workflow-content tests cover PR verification, package smoke, and manual
  package workflow cost controls;
- PR #11, `Add cost-aware GitHub Actions CI`, merged into `main`;
- PR #11 remote `PR Verification` passed on GitHub before merge:
  - `Detect Relevant Changes`;
  - `Build And Test Windows`;
  - `PR Verification Result`.
- post-merge `Package Smoke` on `main` passed and uploaded unsigned MSI smoke
  artifacts.

Node 24 GitHub Actions verification on 2026-05-17:

- PR #12, `Opt GitHub Actions into Node 24`, merged into `main`;
- workflow-content tests require the Node 24 opt-in flag on all GitHub Actions
  workflows;
- PR #12 remote `PR Verification` passed on GitHub with JavaScript actions
  forced to run on Node 24;
- post-merge `Package Smoke` on `main` passed with `checkout`, `setup-dotnet`,
  and `upload-artifact` forced to run on Node 24.

Coolify staging container deploy branch verification on 2026-05-17:

- branch `codex/staging-coolify-container-deploy` adds a Platform API
  Dockerfile for Coolify repo builds, root `.dockerignore` and `.gitignore`
  secret-file guards, staging env template, fallback PostgreSQL compose
  definition, and
  `docs/operations/coolify-staging-deploy.md`;
- targeted invariant tests for the Coolify container deploy content passed
  locally:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~CoolifyContainerDeploymentTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 6 passed, 0 failed, 0 skipped.
- full local solution build passed with 0 warnings and 0 errors:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full local no-build solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 636 passed, 0 failed, 0 skipped.
- Platform API Release publish passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' publish src/AFK4.Platform.Api/AFK4.Platform.Api.csproj -c Release --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -o artifacts/platform-api-publish-check -v minimal
  ```

- fallback PostgreSQL compose config rendered successfully with a dummy local
  password used only for syntax validation:

  ```powershell
  $env:AFK4_STAGING_POSTGRES_PASSWORD = 'dummy-config-check-only'
  docker compose -f deploy/coolify/staging-postgres.fallback.compose.yaml config
  ```

- after Docker Desktop was started, the Platform API image built successfully
  from the Coolify repo-root context:

  ```powershell
  docker build -f src/AFK4.Platform.Api/Dockerfile -t afk4-platform-api:staging-check .
  ```

- local container smoke passed:
  - API container ran as non-root user `app`, listened on port `8080`, and
    returned `status = ok` from `/api/health` through host port `18080`;
  - fallback PostgreSQL compose resource became healthy with no public port
    published;
  - EF migrations were applied from a Linux .NET SDK one-off container on the
    same Docker network after an explicit restore inside that container;
  - PostgreSQL `__EFMigrationsHistory` contained 9 migrations through
    `20260514081906_AddUpdateRollouts`;
  - API container connected to PostgreSQL and returned the expected HTTP 401
    for a DB-backed sign-in attempt with a missing staff user;
  - staging connection strings now include `GSS Encryption Mode=Disable` to
    avoid harmless `libgssapi_krb5.so.2` fallback noise in minimal Linux
    runtime containers.
- a later repeat full runtime image rebuild reached MCR but failed on an
  external `403 Forbidden` metadata response for
  `mcr.microsoft.com/dotnet/aspnet:10.0`; the Dockerfile build stage still
  rebuilt successfully from the current tree with:

  ```powershell
  docker build --target build -f src/AFK4.Platform.Api/Dockerfile -t afk4-platform-api:staging-build-stage-check .
  ```

Coolify VPS staging rehearsal on 2026-05-17:

- branch `codex/coolify-staging-rehearsal` created the first real Coolify
  staging resources:
  - Coolify project `AFK4`, environment `staging`;
  - application `afk4-platform-api-staging`;
  - Coolify-managed PostgreSQL `afk4-staging-postgres`;
  - temporary public staging host
    `https://afk4-staging.207.180.237.97.sslip.io`.
- real staging DNS/TLS was configured after the initial rehearsal:
  `afk4.staging.mubi.dev` resolves to `207.180.237.97`, and
  `curl.exe -i https://afk4.staging.mubi.dev/api/health` returns HTTP 200
  without `curl -k`.
- preferred Coolify-managed PostgreSQL was used, not the fallback compose
  resource.
- EF migrations were applied explicitly from the release workstation after a
  temporary PostgreSQL public port was opened and then closed. Migration
  verification listed all 9 migrations through
  `20260514081906_AddUpdateRollouts`; `Test-NetConnection` confirmed the
  temporary port was closed after verification.
- Initial deploy attempts exposed two runbook/container details:
  - Coolify Dockerfile health checks execute from inside the container and need
    both `curl` and `wget` available in this Coolify version;
  - `AllowedHosts` must include `localhost` and `127.0.0.1` because Coolify's
    in-container health check calls `http://localhost:8080/api/health`.
- the branch updates the Platform API Dockerfile and Coolify runbook/template
  to cover those findings, and keeps invariant coverage in
  `CoolifyContainerDeploymentTests`.
- Coolify deploy `r9ahy05ujzwk0hjzhxpdfhlc` completed successfully for branch
  `codex/coolify-staging-rehearsal`; app and database status were both
  `running:healthy`.
- smoke evidence:

  ```powershell
  curl.exe -k -i --max-time 30 https://afk4-staging.207.180.237.97.sslip.io/api/health
  curl.exe -i --max-time 30 https://afk4.staging.mubi.dev/api/health
  ```

  Result: HTTP 200 with `{"status":"ok",...}`. The real staging domain passes
  TLS validation without the insecure curl flag.

  ```powershell
  curl.exe -k -i --max-time 30 -H "Content-Type: application/json" --data-binary "@-" https://afk4-staging.207.180.237.97.sslip.io/api/auth/staff/sign-in
  ```

  Result: HTTP 401 for a missing staff user, proving the API reached the
  migrated PostgreSQL database rather than failing with a database error.
- post-hardening verification after the Coolify API token, staging database
  password, and session signing key were rotated by the operator:

  ```powershell
  Resolve-DnsName afk4.staging.mubi.dev
  curl.exe -i --max-time 30 https://afk4.staging.mubi.dev/api/health
  curl.exe -i --max-time 30 -H "Content-Type: application/json" --data-binary "@-" https://afk4.staging.mubi.dev/api/auth/staff/sign-in
  ```

  Result: DNS still resolved to `207.180.237.97`, health returned HTTP 200
  with `status = ok`, and fake staff sign-in returned HTTP 401 against the
  rotated database/session configuration.
- local verification for the branch:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~CoolifyContainerDeploymentTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted invariant tests passed, and solution build passed with
  0 warnings and 0 errors.

Real-device smoke preparation branch verification on 2026-05-17:

- branch `codex/real-device-smoke` adds
  `docs/operations/real-device-windows-pc-smoke.md`, links it from
  `README.md`, and adds invariant coverage in
  `RealDeviceSmokeRunbookTests`;
- the Agent Service now references
  `Microsoft.Extensions.Hosting.WindowsServices` and calls
  `AddWindowsService` with service name `AFK4.Agent.Service`, matching the
  WiX service registration used by the gaming-PC MSI;
- targeted red/green verification for the new real-device smoke invariants
  passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~RealDeviceSmokeRunbookTests -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 2 passed, 0 failed, 0 skipped.
- full local solution build passed with 0 warnings and 0 errors:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full local no-build solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 638 passed, 0 failed, 0 skipped.

The runbook preparation does not claim the real Windows PC smoke has been
executed. It explicitly requires operator-performed PC steps, screenshots/log
evidence, and pass/fail recording before physical-hardware hardening can be
closed.

Staging Gaming PC setup bootstrapper branch verification on 2026-05-17:

- branch `codex/staging-gaming-pc-bootstrapper` adds a staging-only one-click
  Windows setup executable path for clean Windows 11 smoke VMs:
  `src/AFK4.GamingPc.Setup` plus testable orchestration in
  `src/AFK4.GamingPc.Setup.Core`;
- the setup executable targets `https://afk4.staging.mubi.dev`, the current
  staging organization, and the current staging branch; it asks only for staff
  username/password, creates a short-lived enrollment code, enrolls the current
  VM, installs the bundled Gaming PC MSI, writes Agent machine configuration,
  starts `AFK4.Agent.Service`, and waits for backend heartbeat evidence;
- `scripts/build-client-packages.ps1` can now emit
  `artifacts/client-packages/afk4-gaming-pc-setup-<version>-<channel>.exe`
  when supplied `-StagingLeasePublicKeyPath` from outside the repository;
- the gaming-PC package publish path now publishes Agent Service and Player
  Shell as self-contained `win-x64` outputs so a clean VM does not need a
  separate .NET Desktop Runtime install before the MSI can run;
- targeted setup tests passed locally:

  ```bash
  dotnet test tests/AFK4.GamingPc.Setup.Tests/AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 9 passed, 0 failed, 0 skipped.
- targeted packaging invariant test passed locally:

  ```bash
  dotnet test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~BuildClientPackagesScript_PublishesStagingGamingPcSetupExeWithEmbeddedMsi" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 1 passed, 0 failed, 0 skipped.
- setup project build passed locally:

  ```bash
  dotnet build src/AFK4.GamingPc.Setup/AFK4.GamingPc.Setup.csproj -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- setup single-file `win-x64` publish with embedded dummy MSI and dummy public
  key resources passed locally, producing `AFK4.GamingPc.Setup.exe`; real VM
  packaging still requires the real Gaming PC MSI on the Windows release
  workstation.

- full solution build passed in this Linux shell when Windows targeting was
  explicitly enabled:

  ```bash
  dotnet build AFK4.sln -p:EnableWindowsTargeting=true -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

- full solution tests were attempted with `EnableWindowsTargeting=true` but
  could not complete in this Linux shell because the existing WPF test
  assemblies require `Microsoft.WindowsDesktop.App` and existing release
  automation tests invoke `powershell.exe`; these remain Windows-runner
  verification items rather than product failures.
- the staging public lease verification key is now committed at
  `deploy/coolify/staging-session-signing-public.pem`; the matching private key
  is stored only in Coolify as `Sessions__SigningPrivateKeyPem`.
- after the first Windows 11 VM bootstrapper attempt failed because the setup
  exe did not contain the embedded public-key resource, the build script was
  fixed to resolve `-StagingLeasePublicKeyPath` to an absolute path before
  passing it to MSBuild; the targeted packaging invariant test was verified
  red/green, the setup exe was rebuilt with the repo-relative key path, and
  binary inspection confirmed both the public-key resource name and public-key
  PEM payload are present in the generated exe.
- a Windows 11 VM smoke on `DESKTOP-DTMPO0V` then enrolled successfully against
  staging as device `3ba8737c-f94b-4ea4-bc25-014af468784f`; the setup UI
  observed backend heartbeat evidence, and staging device diagnostics showed
  the device online with Agent/Shell version `0.1.0`.
- the same VM exposed a packaging defect after install: WiX produced a 32-bit
  MSI, so Windows installed the Agent under `C:\Program Files (x86)\AFK4\...`
  while the bootstrapper machine config pointed
  `Agent__PlayerShellExecutablePath` at `C:\Program Files\AFK4\Player Shell`.
  `scripts/build-client-packages.ps1` now passes `-arch x64` for the
  gaming-PC MSI; the regression test was verified red/green, the staging setup
  exe was rebuilt, MSI summary metadata reports `x64;0`, and administrative
  extraction confirms `PFiles64\AFK4\Player Shell\AFK4.Player.Shell.exe` is
  present.
- the rebuilt x64 bootstrapper was rerun on the same Windows 11 VM. The Agent
  service installed under `C:\Program Files\AFK4\Agent Service`, the Player
  Shell executable existed under `C:\Program Files\AFK4\Player Shell`, and the
  setup UI enrolled staging device `bf44adb1-0681-49f5-81cc-7ceec3d371a7`
  with backend heartbeat evidence.
- a real staging session smoke then passed on that VM:
  session `90531bf3-3f37-4112-9c6a-7682e498fb9f` started as `active`,
  produced an `unlock` command accepted by the Agent, wrote
  `session-lease.json` plus `runtime-state.json` with `state=active`, and the
  visible Player Shell showed `Session is active` with remaining time.
  Ending the session produced an accepted `lock` command, cleared
  `session-lease.json`, wrote `runtime-state.json` with `state=locked`, and the
  visible Shell returned to locked.
- one manual Player Shell visibility gap was observed: the service had started
  an additional `AFK4.Player.Shell.exe` in session `0` as `NT AUTHORITY\SYSTEM`,
  while the manually visible Shell ran in the interactive user session. The
  session-0 Shell could receive named-pipe state first, leaving the visible
  Shell stale. Killing both Shell processes and restarting the visible Shell
  allowed it to receive the active/locked state. Treat this as a Player Shell
  service-supervision hardening item.
- after the lock smoke, the same staging session was manually moved from
  `ending` back to `active` in Coolify PostgreSQL only to leave the VM in an
  active visible Shell state for continued inspection. This SQL reactivation is
  not a valid production or pilot operator path. At the time, it highlighted
  that the session lifecycle needed a normal `ending`
  completion/reconciliation path so a seat/device could be reused without
  manual database edits.

Player Shell and session end hardening branch verification on 2026-05-17:

- branch `codex/staging-gaming-pc-bootstrapper` now disables Agent Service
  Player Shell auto-start by default and gates any future auto-start behind an
  explicit `Agent__PlayerShellAutoStartEnabled=true` setting plus an
  interactive user-session check. The Agent still publishes Shell state over
  the named pipe, so the current smoke/pilot path is to launch the visible
  Player Shell from the logged-in Windows desktop session.
- backend command-result processing now finalizes an `ending` session to
  `ended` when the Agent reports the matching `lock` command as accepted or
  completed. The finalization clears `CurrentLeaseId`, writes `EndedAtUtc`, and
  records a `session-ended` event. Duplicate accepted lock results do not
  create duplicate finalization events, and a second session can start on the
  same seat/device after finalization.
- a 2026-05-17 staging VM re-smoke exposed a recovery case where accepted lock
  results were already persisted while the session remained `ending`. The
  heartbeat command planner now treats an accepted/completed matching lock as a
  finalization signal before planning another lock, so stale `ending` sessions
  can converge to `ended` on the next heartbeat without SQL cleanup.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellProcessSupervisorTests|FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~DeviceCommandEndpointTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~EfHeartbeatSessionCommandPlannerTests|FullyQualifiedName~DeviceCommandEndpointTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: Agent targeted tests passed 8/8; device command endpoint tests passed
  16/16 before the heartbeat fallback and the combined heartbeat planner/device
  command endpoint regression set passed 23/23 after it; full solution build
  completed with 0 warnings and 0 errors; full no-build solution tests passed
  655/655; `git diff --check` was clean.
- at this point the branch had partial Windows 11 staging VM evidence: the
  rebuilt setup enrolled, no service-session Player Shell was running before
  the visible manual Shell launch, session start/unlock worked, and
  physical/visible lock was observed. Session reuse still needed a repeat after
  the heartbeat finalization fallback was deployed; that VM reuse gap is
  addressed by the 2026-05-18 post-redeploy smoke below.

Interactive Player Shell auto-start hardening on 2026-05-18:

- branch `codex/staging-gaming-pc-bootstrapper` now re-enables Player Shell
  auto-start by default, but no longer starts Shell from service session `0`.
  The Agent resolves the active interactive Windows console session, checks for
  an existing Shell process in that same session, and launches Shell there
  through the Windows user-token path. If no interactive session exists, Shell
  launch is skipped while heartbeat and named-pipe state publishing continue.
- process detection is now session-aware, so a stale or accidental
  `AFK4.Player.Shell.exe` in session `0` does not satisfy supervision for the
  logged-in desktop session.
- the staging Gaming PC setup writer explicitly sets
  `Agent__PlayerShellAutoStartEnabled=True` for rebuilt smoke packages.
- the real-device smoke runbook now expects Agent-driven interactive-session
  Shell auto-start. Manual Shell launch is a diagnostic fallback only after an
  auto-start or duplicate-process failure is recorded.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter FullyQualifiedName~PlayerShellProcessSupervisorTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~PlayerShellProcessSupervisorTests|FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.GamingPc.Setup.Tests\AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: Player Shell process supervisor tests passed 9/9; targeted
  Agent Shell/runbook tests passed 11/11; Gaming PC setup tests passed 9/9;
  full solution build completed with 0 warnings and 0 errors; full no-build
  solution tests passed 658/658; `git diff --check` was clean.

Staging VM Shell state delivery finding on 2026-05-18:

- a rebuilt staging Gaming PC setup executable installed and enrolled Windows
  11 VM device `8999549a-e1c1-4b25-8df5-ae8111d8fb97`; backend heartbeat
  reported the device online, and Agent auto-started
  `AFK4.Player.Shell.exe` in interactive user session `1` for `vm\mubi`, not
  service session `0`.
- after a staging SQL cleanup of stale session
  `329f4d61-f9c1-49f1-a3c5-6b91651ad35f`, API session start passed for
  session `992624cf-77d1-413b-8e51-6f88872183eb`; unlock command
  `4b8ac0c7-b872-4274-9379-3f7d61b79ee9` was accepted, floor map moved to
  `Active`, device lock state moved to `false`, and the VM wrote active
  `runtime-state.json` plus matching `session-lease.json`.
- the visible Player Shell remained on its initial locked screen until the
  Shell process was restarted. This confirmed a named-pipe timing race in the
  Agent-to-Shell state path: the old Agent state publisher opened a pipe only
  during a short publish window, so a running Shell could miss the active state.
- the Agent state publisher now keeps a long-lived named-pipe server with the
  latest Player Shell state. Late or restarted Shell clients receive the latest
  state on connection instead of relying on a short timing window.
- after rebuilding the staging Gaming PC setup with the long-lived state pipe,
  a second VM enrollment produced device
  `c5bda42b-77ea-4794-8523-72029c234541`. The backend reported heartbeat
  online, a manual staging SQL assignment attached it to the smoke seat, and
  session `a02f6e5e-a1aa-4dbf-8075-eaecf24efccf` started successfully. The
  visible Shell auto-started in session `1` and changed to `Session is active`
  without manual restart; VM `runtime-state.json` and `session-lease.json`
  matched the backend session id.
- ending that session initially produced lock command
  `e950241c-23c8-481f-b6a7-e302a13646cc`; the Agent accepted the command,
  local device state returned locked, and the Shell returned locked. Before
  staging was redeployed from the branch, the deployed backend still left the
  session in `Ending`.
- after Coolify staging was redeployed to commit
  `560c8a17448a52e33e366d7a0abd2990005019d5`, health returned HTTP 200 and the
  stale `Ending` session converged to locked/ended without SQL. A no-SQL reuse
  smoke then passed on the same VM/seat: session
  `06ccc56c-c615-48f0-822c-ff9e3313c2a9` started active, ended, and the floor
  map returned to `Locked` on the first heartbeat poll; session
  `6ec17520-45f3-4f63-a30d-8685d4ee5fc8` then started on the same seat/device
  without SQL cleanup. Cleanup ending of session
  `6ec17520-45f3-4f63-a30d-8685d4ee5fc8` also returned the seat to `Locked` on
  the first heartbeat poll.
- after PR #18 was merged and Coolify staging was redeployed from `main` commit
  `97dcd870d62b57eb4e6865612ada23ea9d735a22`, public health returned HTTP
  200 at `2026-05-18T09:20:08Z`. A second no-SQL main-deploy reuse smoke passed
  with run id `20260518092114`: session
  `47902334-8536-4883-98f2-f65d6e55cf6e` started active, unlock command
  `cb5f8f0c-7085-44e1-98c8-212c0c580959` was accepted, end produced lock
  command `b889e576-66ca-4137-b91d-bb5d5cf6142e`, and the seat/device reached
  `Locked` on the second poll. Session
  `d8318f5f-0607-4522-a9b5-5f0ab7324478` then started on the same seat/device
  without SQL cleanup, unlock command `7bb0b05b-4e75-46b1-a33a-038234f3b3a5`
  was accepted, cleanup lock command
  `7c582e80-1c9b-4313-959e-1fec906e9939` was accepted, and the final snapshot
  was `Locked`, no active session, device online, device locked.
- local targeted regression verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~NamedPipePlayerShellStateServerTests|FullyQualifiedName~PlayerShellProcessSupervisorTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted Agent Shell state/supervisor tests passed 11/11; full
  solution build completed with 0 warnings and 0 errors; full no-build solution
  tests passed 660/660.

Pilot device-seat assignment branch verification on 2026-05-18:

- branch `codex/pilot-admin-setup` adds a staff-authorized device-seat
  assignment API at
  `POST /api/branches/{branchId}/devices/{deviceId}/seat-assignment`.
- the endpoint requires the new `devices.seat_assignment.assign` permission,
  granted to owner, branch manager, and technician roles. It writes audit
  records for successful and denied attempts.
- assignment is desired-state/idempotent for the same active device/seat pair:
  repeated requests return the current assignment instead of creating duplicate
  active rows. Conflicting active assignments for the target seat or device are
  detached before the new assignment is written.
- assignment is rejected with `409 Conflict` while the target seat or device
  has an active, paused, or ending session, preserving session/device
  consistency.
- the staging Gaming PC setup executable now assigns the enrolled device to the
  fixed staging smoke seat through this API before installing/configuring the
  Agent. The real-device smoke runbook now uses the API assignment path and
  treats direct SQL as only a one-time fresh staging seed requirement.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~DeviceSeatAssignmentEndpointTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.GamingPc.Setup.Tests\AFK4.GamingPc.Setup.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter "FullyQualifiedName~RealDeviceSmokeRunbookTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-build --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: device-seat assignment endpoint tests passed 6/6; Gaming PC setup
  tests passed 10/10; real-device smoke runbook tests passed 2/2; full solution
  build completed with 0 warnings and 0 errors; full no-build solution tests
  passed 667/667.
- after PR #19 was merged and Coolify staging was redeployed from `main` commit
  `d0335fb760a4a6897220f185428939e3e83b0e22`, public health returned HTTP 200
  at `2026-05-18T09:55:17Z`. The deployed assignment endpoint accepted
  assigning device `c5bda42b-77ea-4794-8523-72029c234541` to smoke seat
  `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275` without SQL and returned assignment
  `c16ab13c-9801-4ac3-9323-31fadd17eff4`; device detail then showed seat
  `REAL-PC-SMOKE-001`, online, and locked.
- the staging setup executable was rebuilt from current `main` and installed
  on the Windows 11 VM, enrolling device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`. The setup flow assigned that device
  to smoke seat `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275` through the Platform API
  and observed backend heartbeat evidence; no direct database edit was used.
  A no-SQL setup smoke then passed: session
  `eb3b44f0-6c2e-4479-b45d-5b0d2c48f7d5` started on the new device, unlock
  command `9bf9d5de-fa4b-446d-b7bb-40ea6aecebe7` was accepted, end lock command
  `7570e4f9-c35e-4557-aece-235baa420254` was accepted, and the seat returned
  to `Locked`. Session `68c787cf-77a3-4615-af2a-d77757bafe30` then started on
  the same seat/device without SQL cleanup, unlock command
  `6893a06e-3150-4e7f-a637-cf52fb6a013c` was accepted, cleanup lock command
  `564a3c9c-aebe-43a5-943a-9ad129101b7e` was accepted, and the final snapshot
  was `Locked`, no active session, new device online, and new device locked.

Centralized staging update rollout smoke on 2026-05-18:

- branch `codex/staging-agent-update-rollout-smoke` adds a Coolify-hosted
  MinIO artifact store for staging update packages. The staging update API is
  reachable at `https://updates.afk4.staging.mubi.dev`, the console is
  reachable at `https://updates-console.afk4.staging.mubi.dev`, and the
  `afk4-updates-staging` bucket is publicly readable for Agent downloads.
- `src/AFK4.Update.Publisher` and the publishing scripts now support
  S3-compatible uploads in addition to local filesystem and presigned
  `http-put` publishing. The GitHub `Package Smoke` workflow can build MSI
  packages, publish them to staging MinIO, sign update metadata, register the
  package with the staging Platform API, and create an internal device rollout.
- the staging Gaming PC setup executable now embeds the committed staging
  update package verification public key and writes
  `Agent__UpdatePackageSigningPublicKeyPem`. The setup defaults now write full
  PowerShell executable paths for install, rollback, and restart helpers so the
  Agent's configured executable existence checks pass on Windows.
- WiX now writes `Agent__AgentVersion` and `Agent__ShellVersion` from the MSI
  package version during install. The Agent recovery service recognizes that an
  interrupted self-update has already succeeded after service restart and
  reports `installed` instead of rolling back. The backend and Agent also
  normalize MSI `ProductVersion` values against prerelease metadata suffixes so
  a package such as `0.1.2-ci-smoke` is not re-offered forever after Windows
  Installer exposes `0.1.2`.
- the first staging rollout (`0.1.2-ci-minio-rollout`) proved download,
  signature verification, installer execution, and service restart, but exposed
  two staging configuration issues: an older enrolled VM lacked the update
  public key, and the installer executable was configured as `powershell.exe`
  rather than the absolute path. It also exposed the MSI/prerelease version
  mismatch above.
- after those fixes, package `449cdcf3-20fd-44cf-a94b-cfe73efcdda1` and
  rollout `8a103701-dfa1-4aec-8c63-71f479201f92` installed exact version
  `0.1.3` on staging VM device `0588fb59-3edb-4704-bbdb-094e12417cf1` through
  the Agent update pipeline. Backend rollout status reached `installed` with
  message `Interrupted update completed before Agent restart.`, device detail
  reported Agent/Shell `0.1.3`, and the VM had non-zero MSI artifacts and
  update logs under `C:\ProgramData\AFK4\Agent`.

Staging remote Gaming PC bootstrap verification on 2026-05-19:

- PRs #27, #28, #29, #30, and #31 replaced manual VM file copying for clean
  staging PCs with a MinIO-hosted remote bootstrap path. The first attempt to
  publish the self-contained setup exe exposed a practical limit: the WPF
  setup exe is about 198 MB and failed through the current public updates
  proxy, while the signed/internal MSI artifact path is already proven.
- The final flow publishes only a small PowerShell bootstrap script and
  manifest under:
  `https://updates.afk4.staging.mubi.dev/afk4-updates-staging/bootstrap/gaming-pc/internal/latest.json`.
  The manifest points to the already-published internal Gaming PC MSI under
  the `agent-service/internal/<version>/` prefix and includes SHA-256 and size
  for both the bootstrap script and MSI.
- `scripts/publish-client-msi-updates.ps1` now retries transient publish
  failures per component, and `Package Smoke` now triggers when that script
  changes.
- The final post-merge `Package Smoke` on `main` passed in workflow run
  `26089632552` after PR #31. It built internal version `0.1.13`, published
  MSI update metadata, published the slash-delimited bootstrap objects,
  registered update packages, and created the staging rollout.
- Public verification after the run:
  - `latest.json` returned component `gaming-pc-bootstrap`, version `0.1.13`,
    and channel `internal`;
  - the bootstrap script URL returned HTTP 200 with `Content-Length: 9705`;
  - the MSI package URL returned HTTP 200 with `Content-Length: 58282628`;
  - the manifest package SHA-256 was
    `c0908cc3a102e07f7a5f0c420653df9a8f0ff62461951c1eb9f865116a22c5cd`.
- A clean Windows 11 VM run against bootstrap version `0.1.13` successfully
  downloaded the remote manifest/script/MSI, enrolled device
  `78bb8909-2183-47b2-abdb-0d92a19f3807`, and assigned smoke seat
  `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275`, but Windows Installer then failed
  with MSI error 1920/1603. The log showed MSI starting
  `AFK4.Agent.Service` before the bootstrap script had written the enrolled
  device credential and Agent machine configuration, causing rollback and
  service removal.
- PR #33 fixed that sequencing by writing Agent machine configuration
  immediately after enrollment and seat assignment, before `msiexec.exe` can
  start the service during MSI installation. The script also rewrites the same
  config after MSI install before the final service start. Local verification
  on 2026-05-19: the PowerShell parser passed for
  `scripts/publish-staging-bootstrapper.ps1`, and targeted
  `ClientReleaseAutomationTests` passed 37/37. Remote `PR Verification Result`
  passed in workflow run `26091209806`, and post-merge `Package Smoke` run
  `26091453388` published remote bootstrap version `0.1.14`.
- The corrected `0.1.14` remote bootstrap passed clean Windows 11 VM smoke on
  2026-05-19. The VM downloaded the remote manifest, bootstrap script, and MSI;
  enrolled device `6cae5721-1aa3-467b-8984-82f5574104b1`; assigned smoke seat
  `9f3adbd3-957e-4dc8-8d34-a6bfa56b9275`; completed MSI install; reported
  backend heartbeat at `2026-05-19T10:39:48Z`; showed Player Shell locked in
  the interactive user session; and reported `AFK4.Agent.Service` running with
  Agent/Shell versions `0.1.14`.
- The same VM then passed the session lifecycle smoke without SQL cleanup:
  backend session `d883bb50-e462-45ea-b620-d6dacd11f005` started on the smoke
  seat, the Agent accepted unlock command
  `f33b104e-f38d-4b1f-a96e-80c591c9a27b`, `runtime-state.json` and
  `session-lease.json` appeared with active state, and the visible Player Shell
  changed to `Session is active.`. Ending that session produced accepted lock
  command `ce1a19e1-8f9b-4b34-a6f3-fb113bac0193`; the VM returned to locked
  state and removed `session-lease.json`.
- Seat/device reuse also passed: backend session
  `2f0fbce3-4a7e-4056-b757-858ffa7adc6a` started on the same seat/device,
  unlock command `da65b11b-bbfd-4f39-b86c-2782a3cc5181` was accepted, the VM
  returned to visible active state, and final end returned the VM to locked
  state with `session-lease.json` removed. The final end exposed a follow-up
  backend issue: two lock commands,
  `d9a26aef-0a31-48c8-ac25-ce17e2269e27` and
  `41eb4810-b088-4bb8-82c4-7120961b82da`, were created and accepted for the
  same session end. This is tracked as GitHub issue #36.
- Issue #36 is fixed in code by making heartbeat lock planning and
  session-reconciliation lock dispatch idempotent when a pending, accepted, or
  completed lock command already exists for the same device/session. Local
  verification on 2026-05-19: targeted planner/reconciliation/session command
  tests passed 29/29, and full `AFK4.Platform.Api.Tests` passed 328/328. The
  fix shipped through PR #38 and was redeployed to Coolify staging deployment
  `vlhc8wmm07nrc8kit64my1y8` from `main` commit
  `ccf938354d7cb86edf2349cf5696a7dd51332136`. The VM recheck then started
  session `1df4e315-9585-47af-9c74-02c2ebe423de`, observed accepted unlock
  command `fa317814-6786-4815-962b-7db9f9dfd023`, ended the session, and
  observed exactly one fresh lock command
  `96f9f759-9f22-466e-9c38-dcaef921bf22`; the seat returned to `Locked` with
  no active session and the device reported locked. GitHub issue #36 was
  closed after this staging evidence.

Operator Pilot Setup UI branch-local verification on 2026-05-19:

- focused Operator App tests:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorPilotSetupApiClientTests|PilotSetupWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 54 passed, 0 failed, 0 skipped.
- full solution build:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: build succeeded with 0 warnings and 0 errors.
- whitespace check:

  ```powershell
  git diff --check
  ```

  Result: clean output with no whitespace errors.

Operator App pilot/dev continuation verification on 2026-05-20:

- after clarifying that physical Windows PC smoke and exposed staging token
  rotation are hardening/ops hygiene rather than current pilot/dev blockers,
  focused Operator App tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --filter "OperatorAppOptionsTests|OperatorPilotSetupApiClientTests|PilotSetupWorkspaceViewModelTests|SettingsWorkspaceViewModelTests|OperatorShellViewModelTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 58 passed, 0 failed, 0 skipped.

- Operator App project build passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Operator.App\AFK4.Operator.App.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: build succeeded with 0 warnings and 0 errors.

- full Operator App test project passed after adding the staging URL runtime
  override:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 165 passed, 0 failed, 0 skipped.

Operator App session-start usability fix on 2026-05-20:

- the previous floor-map context panel defaulted to `postpaid_debt` without a
  player account, which made the visible `Start` action fail with HTTP 400
  (`Player account id is required for session billing`) during staging smoke.
- local code now supports a fast no-ledger guest session start when
  `BillingMode` is blank and no `PlayerAccountId` is supplied; billed modes
  still require player/tariff/package data as appropriate.
- the Operator App context panel now uses a billing mode selector instead of a
  raw billing-mode textbox and hides player/tariff GUID fields for the fast
  guest/no-ledger smoke path.
- focused backend and Operator App verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter "EfSessionBillingIntegrationTests|EfSessionCommandServiceTests" --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: targeted Platform API session tests passed 17/17, Operator App tests
  passed 168/168, full solution build succeeded with 0 warnings and 0 errors,
  and full solution tests passed 740/740. Staging smoke still requires this
  branch to be deployed before the fast guest/no-ledger start path can work
  against `afk4.staging.mubi.dev`.

Operator App redesign branch-local verification on 2026-05-20:

- branch `codex/operator-app-redesign` replaces the previous dense/raw WPF
  shell with an operator-console layout. The floor map now surfaces counts,
  selected-seat state, device summary, and remaining time. POS separates
  catalog and checkout, hides category/product GUIDs from the primary path, and
  formats cart/stock/price text for operators. Player, shift, and settings
  workspaces expose higher-level summaries instead of empty panels and
  backend-shaped inputs.
- a second visual pass after launching the WPF app against staging added POS and
  player empty states, explicit labels for player/money fields, a cash movement
  type selector instead of raw `cash_in` text, shorter branch display in
  Operations, and ellipsis/tooltips for long floor-card names.
- the seat context command flow no longer leaves a successful start/end action
  stuck at `Waiting for backend confirmation`; after the backend accepts the
  command, the pending state is cleared and the existing realtime/floor refresh
  path remains responsible for final seat state.
- local verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' build src\AFK4.Operator.App\AFK4.Operator.App.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: Operator App build succeeded, Operator App tests passed 169/169,
  full solution tests passed 743/743, and `git diff --check` returned clean
  aside from expected CRLF normalization warnings.

- runtime visual smoke against the staging Windows VM is still required before
  treating the redesign as accepted. The redesign intentionally moves raw
  update package registration and other advanced settings controls out of the
  default first-screen path; those operations may need a follow-up advanced
  tools screen if they must remain operator-accessible.

## Known Gaps

- Real Coolify staging now exists and passes backend health/auth smoke on the
  real `afk4.staging.mubi.dev` domain with TLS validation.
- Coolify API token rotation and staging database/session secret rotation were
  completed after the rehearsal. Keep future secret handling out of chat and
  prefer Coolify UI/runtime-only settings for sensitive application variables.
- GitHub Actions workflows are defined and verified, but GitHub rulesets are
  not enforced for the current private repository plan. Until branch protection
  becomes available, PR merges must manually require a green
  `PR Verification Result` on the current head commit, as recorded in
  `AGENTS.md`.
- General staff management workflows, custom roles, and role editing UI are not
  implemented beyond the minimum Pilot Setup panel.
- General Operator App layout management UI is not implemented beyond the
  minimum one-zone/seats Pilot Setup panel.
- Device-seat assignment now has an authorized Platform API path and staging
  setup integration; general Operator App device/seat management UI is not
  implemented beyond optional already-enrolled device assignment in the Pilot
  Setup panel.
- Automatic Agent-side consumption of rotated credentials is not implemented.
- Real Windows PC smoke has a repeatable staging runbook. Physical Windows
  10/11 hardware execution remains recommended hardening before wider rollout,
  but it is no longer treated as a blocker for the current pilot/dev cycle
  because corrected remote bootstrap `0.1.14` and two-session Windows 11 VM
  smoke have passed.
- The staging clean-machine bootstrap path is now remote: VM operators can
  download `latest.json`, verify the small bootstrap script, and run it from an
  elevated PowerShell session without local file sharing or repository access.
  One Windows 11 VM passed the earlier rebuilt x64 setup
  install/enroll/heartbeat plus session start/end and visible Player Shell
  state evidence. The first remote bootstrap VM run exposed the MSI service
  start/config sequencing bug described above; corrected remote bootstrap
  `0.1.14` then passed clean Windows 11 VM install/enroll/seat-assignment/
  heartbeat/locked-Shell smoke plus two session start/end cycles with seat
  reuse. Repeat on a second clean VM or physical Windows PC to broaden
  confidence, not as a prerequisite for continuing Operator App staging tests.
- The staging bootstrap path is for clean machines, not the update path for
  already enrolled PCs. Staging MinIO/internal MSI update rollouts have passed
  on one Windows 11 VM through Agent/Shell `0.1.7`,
  including recovery from stale update state and suppression of superseded
  older rollout offers. The update epic is closed for the current pilot/dev
  cycle; physical hardware update and rollback evidence remain broader release
  hardening rather than a separate next development branch.
- Windows lock/unlock enforcement needs real Windows 10/11 device validation
  beyond adapter-level automated tests; if physical desktop lock/unlock does
  not occur, record that as an enforcement hardening gap rather than as a pass.
- Player Shell service-session competition and missed state delivery are
  mitigated in code and have Windows 11 VM evidence for interactive-session
  auto-start plus active-state delivery without manual Shell restart.
- Session end finalization is implemented in code for accepted/completed lock
  command results and as a heartbeat recovery fallback when an accepted lock is
  already persisted for an `ending` session. Targeted tests cover session reuse,
  duplicate results, and heartbeat convergence; post-redeploy Windows 11 VM
  smoke confirmed no-SQL reuse on the same seat/device. A later remote
  bootstrap `0.1.14` VM smoke also confirmed reuse, but exposed duplicate lock
  command creation on the final session end. Issue #36 is fixed, redeployed to
  staging, and closed after a VM recheck confirmed a single lock command for one
  session end.
- Operator App staging observation can now use
  `AFK4_OPERATOR_PLATFORM_BASE_URL=https://afk4.staging.mubi.dev`; the app
  still defaults to `http://localhost:5074` when the variable is not set.
- The previous Operator App layout was found unusable during staging smoke even
  though start/end session worked. The active follow-up is a full operator
  console redesign branch; do not continue polishing the old raw-form layout.
- Production Authenticode certificate authority/storage is undecided.
- Staging update artifacts are hosted from Coolify MinIO. Production
  object-store/CDN provider, public-read policy, retention, and presigned URL
  automation are still undecided.
- Dedicated service credential policy for update package registration is
  undecided.
- PostgreSQL restore rehearsal has a runbook and scripted helper, and a real
  Coolify staging restore rehearsal completed on 2026-05-19. Production backup
  encryption, retention, off-host storage, and restore ownership are still
  launch decisions.
- The Coolify API token used during the 2026-05-19 restore rehearsal was
  exposed in chat. Rotate it before sensitive staging operations; it is tracked
  as staging secret hygiene, not as a blocker for Operator App testing or
  continued development.
- Production lease duration and heartbeat refresh threshold need tuning after
  real Agent telemetry.

## Recommended Next Work

1. Keep enforcing the manual PR merge rule from `AGENTS.md`: current head
   commit must have a green remote `PR Verification Result`.
2. Continue `codex/operator-app-redesign` from the backend-connected React
   shell by working through the new "Backend Connectivity TODO From Current
   React UI Copy" checklist in
   `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`.
   Every production-visible `Fixture`, `not implemented`, `нет backend`,
   missing-contract, or local-only action state should either be wired to an
   existing Platform API contract or backed by a new backend contract before
   the copy is removed. Then test the WebView2 Operator App against deployed
   staging: sign-in, floor map, booking, POS, clients, payments/shifts, logs,
   settings, session actions against current staging device/seat state, and
   actionable error handling. Preserve the accepted fixture design baseline
   while replacing local state with backend-backed behavior. Treat any
   remaining raw GUID/form surfaces as usability bugs unless they are
   explicitly advanced technician tools.
3. Choose production Authenticode certificate authority/storage, production
   object-store or CDN provider, presigned URL automation, and update
   registration credential policy before commercial release. Rotate any
   staging credentials that were exposed during manual smoke setup as
   operational hygiene before sensitive staging operations.
4. Harden Agent production behavior outside the update epic: rotated credential
   consumption, reboot/lock recovery, and lease timing telemetry.
5. Harden and expand beyond the one-shot Pilot Setup panel into full staff and
   role editing, layout management, device-seat management, tariff/POS
   management, and runtime/staging configuration as needed.
6. Continue physical Windows PC validation for lock/unlock, reboot recovery,
   and remote bootstrap/update behavior now that the VM duplicate-lock
   regression is closed. Treat findings as hardening unless they block the
   current Operator App staging test.

## Recent Integration Notes

- On 2026-05-20, `codex/operator-app-redesign` gained the first WebView2/React
  Operator App implementation: WebView2 startup shell, local asset resolution,
  typed host config injection, Vite/React frontend foundation, first visual
  floor-map console, host/frontend tests, local browser smoke, and desktop app
  launch. WPF remains only as legacy/parity code until WebView2 reaches pilot
  day-flow parity.
- On 2026-05-20, roadmap/progress status was clarified after the Operator
  Pilot Setup UI merge: physical Windows PC smoke and exposed staging Coolify
  token rotation are tracked as hardening/ops hygiene, not blockers for the
  current pilot/dev cycle. The next active validation focus is Operator App
  testing against deployed staging.
- On 2026-05-20, the Operator App gained the
  `AFK4_OPERATOR_PLATFORM_BASE_URL` runtime override so a local build can be
  launched directly against `https://afk4.staging.mubi.dev` for staging smoke
  without a staging-specific rebuild.
- PR #42, `Make operator guest session start usable`, merged into `main` on
  2026-05-20 with merge commit
  `20deffd1aa74412921696405dfcc2c098fc2b410`. The PR head was
  `f47780c761e470578d98a2f80ddb84e60ad14fd3`, and remote
  `PR Verification Result` passed in workflow run `26142746802`. This shipped
  the fast guest/no-ledger session start path, Operator App billing selector,
  staging URL runtime override, and updated pilot/dev roadmap status.
- The post-merge `Package Smoke` workflow on `main` passed in workflow run
  `26142963116`. It built and uploaded internal package/bootstrap version
  `0.1.16`; the public staging bootstrap latest manifest now reports
  `gaming-pc-bootstrap` version `0.1.16`, script SHA-256
  `0cc1163ab847bf242bea5bcd125786990b48f8acc28121e621ffbaac135f0bae`, MSI
  SHA-256 `7c7320cc5755fdbe9ff5c85b980cedfbf867c43321069cca513a3d8a6b9c3e70`,
  and publish time `2026-05-20T05:23:14.0936296Z`.
- On 2026-05-20, staging Operator App smoke confirmed session start/end works
  but the surrounding UI is not viable for pilot operators. The follow-up branch
  `codex/operator-app-redesign` starts a full shell/workspace redesign and
  should be reviewed visually before more feature work depends on the old
  layout.
- PR #41, `Add operator pilot setup ui`, merged into `main` on 2026-05-20 with
  squash merge commit `129fa76fa8354a3f3f693def866e0ed02feedf20`. The PR head
  was `02f7dfcf166150f506b2ba918c3cfbaea86c4625`, and remote
  `PR Verification Result` passed in workflow run `26140087374`. The remote
  branch `codex/operator-pilot-setup-ui` was deleted after merge. The branch
  added the Operator App `Settings` -> `Pilot Setup` surface for minimum pilot
  branch setup: it creates or reuses staff and one layout zone/seats, creates
  tariff/POS setup idempotently for reruns through the same Operator App
  inputs, and can optionally assign an already-enrolled device to a configured
  seat. It does not add a general admin panel or full general-purpose
  management screens; the PowerShell script remains the fallback path.
- PR #9, `Add Authenticode CI update registration flow`, merged into `main` on
  2026-05-16 with merge commit
  `6f11e140d9c45b1592f71dc6c3e056fdb272c710`.
- The feature branch `codex/authenticode-ci-registration` was deleted after
  merge.
- PR #11, `Add cost-aware GitHub Actions CI`, merged into `main` on 2026-05-17
  with merge commit `8bf9d9e7823f5c932ed4ee8dd5d92b855423bdef`.
- PR #12, `Opt GitHub Actions into Node 24`, merged into `main` on 2026-05-17
  with merge commit `aa8e1b9c1d3504e9c64de6a4aa6872692728fe35`.
- PR #15, `Add Coolify staging container deploy path`, merged into `main` on
  2026-05-17 with squash merge commit
  `e1f2623bf24bf6b37d3308e2ec6778bb48fdda6d`. The PR head was
  `d761998eb086f7128bcce08ec414adba927b9fd7`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25982865673`. The remote branch
  `codex/staging-coolify-container-deploy` was deleted after merge.
- PR #16, `Rehearse Coolify staging deploy`, merged into `main` on
  2026-05-17 with squash merge commit
  `e0614f6e68463224e5fca678de6e2a841eb924b6`. The PR head was
  `fb6f1b82cca2a951e0c722f0c71a2fd690f1270f`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25986777751`. The remote branch `codex/coolify-staging-rehearsal` was
  deleted after merge. This completed the first real Coolify staging deploy,
  real staging DNS/TLS verification, post-rotation staging smoke, and
  container health-check hardening.
- PR #17, `Add real device smoke runbook`, merged into `main` on
  2026-05-17 with squash merge commit
  `560be2d84c5bc4099fb35e33a2c1e8a027d72740`. The PR head was
  `ae7d1b8f011017711112998f930e9bca3166f8e4`, and remote
  `PR Verification Result` passed for that head in workflow run
  `25988314062`. The remote branch `codex/real-device-smoke` was deleted after
  merge. This prepared the repeatable staging runbook for one real Windows
  gaming PC and added Windows Service host lifetime wiring for the Agent
  Service, but the real hardware smoke still needs execution and recorded
  evidence.
- PR #18, `Harden Player Shell session supervision`, merged into `main` on
  2026-05-18 with squash merge commit
  `2d7aaf093a889c7831547ef657ea0a2b962b62af`. The PR head was
  `b7f99ff02a4ecf9cc58812680a46b60f3abb86a6`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26023619024`. The remote branch
  `codex/staging-gaming-pc-bootstrapper` was deleted after merge. This added
  the staging Gaming PC setup path, interactive-session Player Shell
  supervision, long-lived Agent-to-Shell state delivery, session
  end/finalization reuse hardening, and Windows 11 VM post-redeploy smoke
  evidence. The remaining runtime hardening is physical Windows hardware smoke
  plus reboot/update recovery.
- PR #19, `Add pilot device-seat assignment API`, merged into `main` on
  2026-05-18 with squash merge commit
  `c5a7f64788997d22a4c2c01b456bfc50cd97f61e`. The PR head was
  `ced20d5e838e7d7eeb81cf5624481e62a485cd81`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26025698119`. The remote branch `codex/pilot-admin-setup` was deleted after
  merge. This added the staff-authorized device-seat assignment API, staging
  setup assignment through the API, and runbook updates so future staging smoke
  device assignment does not require direct PostgreSQL edits.
- PR #20, `Add staging MinIO update rollout smoke`, merged into `main` on
  2026-05-19 with squash merge commit
  `ed93e0dae009fcade9b081044641e6970abb80a6`. The PR head was
  `1bc1a6346cb4ff00e05e288cfc0d58fc24adcb42`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26077229854`. The first PR run exposed a Windows hosted-runner timeout in a
  PowerShell release automation test; the branch increased that test timeout
  and the rerun passed.
- after PR #20 was merged, Coolify staging was redeployed from `main` commit
  `ed93e0dae009fcade9b081044641e6970abb80a6` through deployment
  `yvl8trys8d0o6tgffd8wgawj`, which finished successfully. Public health
  returned HTTP 200 with `status = ok` at `2026-05-19T05:11:34Z`.
- the post-merge `Package Smoke` workflow on `main` passed in workflow run
  `26077441822`. It built internal MSI version `0.1.6`, published the Gaming
  PC MSI to staging MinIO for both `agent-service` and `player-shell`,
  registered update package requests with staging, and created Agent Service
  device rollout `830b5a2e-2eed-47c2-b3c8-411f05b09edf` for staging device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`.
- the Windows 11 VM later installed Agent/Shell `0.1.6` from that rollout.
  The VM also exposed a stale recovery-state bug: after the successful `0.1.6`
  install, Agent recovery attempted the older `0.1.3` MSI from an old
  recoverable state file and Windows Installer rejected the downgrade with
  exit code `1603`. Branch `codex/update-recovery-superseded-state` fixes this
  by marking recoverable update states as `superseded` when the installed
  component version is already newer than the stale target, avoiding rollback
  or installer execution for older MSI artifacts.
- the same branch hardens update artifact download for sleep, reboot, network
  loss, and partial file cases. The Agent now downloads to a temporary file,
  deletes temporary partials on failure, removes stale wrong-sized final
  artifacts before retry, moves the completed artifact into place atomically,
  and can reuse an already complete staged artifact after restart.
- PR #21, `Handle superseded update recovery states`, merged into `main` on
  2026-05-19 with squash merge commit
  `55fa0911eea8c06c7b5f4315e2452edcdda5f3f1`. The PR head was
  `8a8ebfa844be4e0805cbd66c151dff4d28be8a60`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26079092131`. The remote branch
  `codex/update-recovery-superseded-state` was deleted after merge.
- after PR #21 was merged, Coolify staging was redeployed from `main` commit
  `55fa0911eea8c06c7b5f4315e2452edcdda5f3f1` through deployment
  `cb1ncgnenndmkyaytowf3n3f`, which finished successfully. Public health
  returned HTTP 200 with `status = ok` at `2026-05-19T06:03:13Z`.
- the post-merge `Package Smoke` workflow on `main` passed in workflow run
  `26079268014`. It built internal MSI version `0.1.7`, published the Gaming
  PC MSI to staging MinIO for both `agent-service` and `player-shell`, and
  created Agent Service device rollout
  `84c8be00-2cfd-4e8a-8483-e78618481cbc` for staging device
  `0588fb59-3edb-4704-bbdb-094e12417cf1`. A direct HEAD request to the
  published Agent Service MSI returned HTTP 200 with a non-zero
  `Content-Length` of `58278532`.
- the Windows 11 VM then installed Agent/Shell `0.1.7` without a forced update
  check. The staged MSI
  `agent-service-0.1.7-c0e8e0ec3f214edb935fdac0c682a1f6.msi` had length
  `58278532`, the `0.1.7` install log ended with Windows Installer exit code
  `0`, and the `0.1.7` state file reported `installed`.
- the same VM run exposed one remaining update-offer bug: after `0.1.7` was
  installed, the backend still offered an older active `0.1.3` rollout because
  `/updates/check` only skipped exact/current versions, not versions older than
  the installed component. PR #22, `Filter superseded update offers`, merged
  into `main` on 2026-05-19 with squash merge commit
  `9e7153426b3cf1619e0c4967d3e0745734114561`. The PR head was
  `5f5ae02`, and remote `PR Verification Result` passed for that head in
  workflow run `26079933935`. The branch updated Platform API check logic so
  active older rollouts are not offered when the device already reports a
  newer MSI-compatible version.
- after PR #22 was merged, Coolify staging was redeployed from `main` commit
  `9e7153426b3cf1619e0c4967d3e0745734114561` through deployment
  `a12dby9ibmavauz88o01b6lv`, which finished successfully. Public health
  returned HTTP 200 with `status = ok` at `2026-05-19T06:27:41Z`.
- after that redeploy, the same Windows 11 VM restarted the Agent Service and
  waited one full 360 second polling window. Agent/Shell remained `0.1.7`, and
  the old `agent-service-0.1.3-install.log` kept its previous
  `2026-05-19 11:10:01` local timestamp and `41052` byte length. This confirms
  the Platform API offer filter stopped re-offering older active rollouts to a
  device that already reports a newer installed version.
- staging update rollout cleanup was then performed through the Platform API,
  not direct PostgreSQL edits. Rollouts `8a103701-dfa1-4aec-8c63-71f479201f92`
  (`0.1.3`), `830b5a2e-2eed-47c2-b3c8-411f05b09edf` (`0.1.6`), and
  `84c8be00-2cfd-4e8a-8483-e78618481cbc` (`0.1.7`) were moved from `active`
  to `completed`; all internal staging Agent Service update rollouts are now
  terminal.
- branch `codex/pilot-admin-setup-api` adds the first API-driven pilot setup
  surface for configuration that previously required seeding or direct data
  manipulation: owner-authorized staff user creation with predefined role
  assignments, branch-manager layout zone/seat creation and listing, a
  PowerShell `scripts/configure-pilot-branch.ps1` setup script, and
  `docs/operations/pilot-branch-setup.md`. Existing APIs already cover tariff
  creation/versioning, POS category/product setup, and device-seat assignment,
  so the script composes those paths without SQL.
- PR #23, `Add pilot branch setup API`, merged into `main` on 2026-05-19 with
  squash merge commit `c5281cef06b65d0e23c2443d8208f94244f94a94`. The remote
  `PR Verification Result` passed in workflow run `26082377316`. Coolify
  staging was redeployed through deployment `whzxa26tnkyhwocyrwgseezu`, which
  finished successfully, and public health returned `status = ok` at
  `2026-05-19T07:27:24Z`.
- staging smoke after PR #23 showed the new setup endpoints were deployed, but
  also exposed that the existing staging smoke user is a branch manager and
  could not create cashier/technician staff because the first endpoint version
  required owner-only role management.
- PR #24, `Harden pilot setup script and branch staff setup`, merged into
  `main` on 2026-05-19 with squash merge commit
  `ca2730411585671d8632da6cdcb26fdf92552bfb`. The remote
  `PR Verification Result` passed in workflow run `26083191829`. It adds a
  branch-scoped staff setup permission for owner and branch manager roles,
  keeps the owner role out of the pilot branch staff shortcut, and changes
  `scripts/configure-pilot-branch.ps1` to use `curl.exe` with explicit
  timeout/status handling instead of PowerShell web cmdlets.
- after PR #24 was merged, Coolify staging was redeployed from `main` commit
  `ca2730411585671d8632da6cdcb26fdf92552bfb` through deployment
  `m9ut1d5y5d269qmri1zy25br`, which finished successfully. Public health
  returned `status = ok` at `2026-05-19T07:45:15Z`.
- the pilot setup script then completed against staging without SQL using the
  existing branch manager smoke account. It created or reused cashier staff
  `b36f91a8-d316-4903-85a7-fa9743e28337`, technician staff
  `8b87a516-46fe-43a1-a51b-816371921c93`, zone
  `ded68227-f15c-45a0-8387-0ea309984d34`, 10 seats, tariff
  `ddd5ee34-08ff-4b9b-8da5-bf2773ca0540`, tariff version
  `b1ecd790-8d69-4bd9-abbc-96f4e8db7102`, POS category
  `ae04cc06-751b-4ae8-b67f-098d744a780e`, and POS product
  `de357405-7041-4e88-a55c-8bf4d9fc3ca9`. A follow-up sign-in smoke for
  `cashier.pilot@afk4.test` confirmed one branch assignment and
  `sessions.start` permission.
- PR #27, `Publish staging bootstrapper to MinIO`, merged into `main` on
  2026-05-19 with squash merge commit
  `bc099e5064b0a2b0d1cfa63e336ab98e92dbd913`. Remote
  `PR Verification Result` passed in workflow run `26086303266`, but the
  post-merge `Package Smoke` run `26086548607` failed while uploading the
  198 MB self-contained setup exe through the public updates endpoint.
- PR #28, `Publish remote staging gaming PC bootstrap`, merged into `main` on
  2026-05-19 with squash merge commit
  `bbfb78550edf628f6f1cd6250431d0d90b813360`. Remote
  `PR Verification Result` passed in workflow run `26087269283`. The branch
  changed the clean-machine path to a small remote PowerShell bootstrap script
  that composes the already-published internal Gaming PC MSI.
- PR #29, `Retry staging package publishing`, merged into `main` on
  2026-05-19 with squash merge commit
  `6a5450fd4a805b4f7b97503f016078ee48584d5e`. Remote
  `PR Verification Result` passed in workflow run `26087858682`; it added
  retry/backoff around each update package publisher invocation.
- PR #30, `Trigger package smoke on publish script changes`, merged into
  `main` on 2026-05-19 with squash merge commit
  `9d21bc3fba90f54541d97e8b00b81d87e9d340b2`. Remote
  `PR Verification Result` passed in workflow run `26088166185`; it added
  `scripts/publish-client-msi-updates.ps1` to the `Package Smoke` path filter.
  The post-merge `Package Smoke` run `26088513440` passed but revealed that
  bootstrap objects were uploaded with spaces in their keys, so the public
  slash-delimited manifest URL returned 404.
- PR #31, `Fix staging bootstrap S3 object keys`, merged into `main` on
  2026-05-19 with squash merge commit
  `6362aa4c3478559dd9867c96cc37ee34f5e630d8`. Remote
  `PR Verification Result` passed in workflow run `26089331575`. The final
  post-merge `Package Smoke` run `26089632552` passed and published
  `gaming-pc-bootstrap` version `0.1.13` at the public latest manifest URL:
  `https://updates.afk4.staging.mubi.dev/afk4-updates-staging/bootstrap/gaming-pc/internal/latest.json`.
- PR #33, `Fix staging bootstrap service config order`, merged into `main` on
  2026-05-19 with squash merge commit
  `ce647b1f415470cd9e783f745f513d17d67f9100`. The PR head was
  `bb1df17bc8ba6d0c7825d5a07245b85597b38eac`, and remote
  `PR Verification Result` passed for that head in workflow run
  `26091209806`. Post-merge `Package Smoke` run `26091453388` passed and
  published remote bootstrap `0.1.14`; public verification returned
  `latest.json` component `gaming-pc-bootstrap`, script `Content-Length: 9892`,
  MSI `Content-Length: 58282628`, and package SHA-256
  `7446338abed783712d801dd2c01615aab26e5b3c0787dc42e379b567477f5383`.
- PR #35, `Record bootstrap 0.1.14 VM smoke pass`, merged into `main` on
  2026-05-19 with squash merge commit
  `73cd164004f3dabb8ee2588f8783fbfce59e1d00`; it recorded the clean VM remote
  bootstrap pass for `0.1.14`.
- PR #37, `Record remote bootstrap session smoke`, merged into `main` on
  2026-05-19 with squash merge commit
  `962da209e56248933be4a3d20ac87acbd367d450`; it recorded the two-session VM
  smoke and opened follow-up issue #36 for duplicate lock command planning.
- PR #38, `Fix duplicate lock command planning`, merged into `main` on
  2026-05-19 with squash merge commit
  `ccf938354d7cb86edf2349cf5696a7dd51332136`. Remote
  `PR Verification Result` passed in workflow run `26093286571`; Coolify
  staging deployment `vlhc8wmm07nrc8kit64my1y8` finished on the same commit.
  The follow-up Windows 11 VM smoke started session
  `1df4e315-9585-47af-9c74-02c2ebe423de`, accepted unlock command
  `fa317814-6786-4815-962b-7db9f9dfd023`, ended it, and observed one fresh lock
  command `96f9f759-9f22-466e-9c38-dcaef921bf22` before issue #36 was closed.

PostgreSQL restore rehearsal helper branch verification on 2026-05-19:

- branch `codex/postgres-restore-rehearsal` adds
  `scripts/rehearse-postgres-restore.ps1` plus runbook/progress/roadmap
  updates for a repeatable PostgreSQL backup, restore, migration update, and
  table-count rehearsal path. The script supports host-installed PostgreSQL
  client tools and `-PostgresClientMode docker` for release machines that have
  Docker but no `pg_dump`/`pg_restore`/`psql` in `PATH`;
- the branch also fixes `PlatformDbContextDesignTimeFactory` so `dotnet ef`
  respects `ConnectionStrings__PlatformDatabase` instead of always using
  `localhost:5432`, which is required for restore rehearsals on non-default
  PostgreSQL targets;
- TDD red/green evidence: the new
  `PostgresRestoreRehearsalScriptTests` first failed because the script,
  Docker mode, EF target parameter, and runbook references did not exist, then
  passed after implementation. `PlatformDbContextDesignTimeFactoryTests` first
  failed because the design-time factory ignored the environment connection
  string, then passed after the fix;
- local focused verification passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Agent.Service.Tests\AFK4.Agent.Service.Tests.csproj --filter PostgresRestoreRehearsalScriptTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Platform.Api.Tests\AFK4.Platform.Api.Tests.csproj --filter PlatformDbContextDesignTimeFactoryTests --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 5 passed, 0 failed, 0 skipped across the two focused commands.
- dry-run verification with dummy PostgreSQL URLs passed and redacted URL
  credentials from output. A negative dry-run with `BackupRoot` under
  `D:\afk4.net\artifacts` failed as expected with the repository dump guard.
- after Docker Desktop was started, a local Docker-based restore rehearsal
  passed against a temporary `postgres:17-alpine` container on
  `127.0.0.1:55432`: source `afk4_dev` was migrated through all 9 EF
  migrations, `scripts/rehearse-postgres-restore.ps1 -PostgresClientMode
  docker` created a custom-format backup under `D:\afk4-backups-local`, verified
  the archive catalog, generated the idempotent EF migration script under
  ignored `artifacts/`, restored into `afk4_restore_rehearsal`, ran EF database
  update with no pending migrations, and sampled row counts for
  `ledger_entries`, `audit_records`, `devices`, `sessions`, and
  `update_packages`;
- restored database verification showed 9 migrations through
  `20260514081906_AddUpdateRollouts`. Platform API post-restore smoke against
  the restored database returned `health=ok` from `/api/health` and HTTP 401
  for a fake staff sign-in, proving the API reached PostgreSQL;
- a real Coolify staging restore rehearsal then completed against
  `afk4-staging-postgres`. Coolify API reported version `4.0.0`, project
  `AFK4`, application `afk4-platform-api-staging`, and database
  `afk4-staging-postgres` as `running:healthy`. The database public TCP proxy
  was temporarily enabled on port `55432` through the Coolify API, then restored
  to `is_public=False` after the rehearsal;
- the staging rehearsal created a custom-format backup under
  `D:\afk4-backups-staging`, verified the archive catalog, created and restored
  into temporary database `afk4_restore_rehearsal`, ran EF database update with
  no pending migrations, sampled table counts, verified 9 migrations through
  `20260514081906_AddUpdateRollouts`, smoke-tested Platform API against the
  restored database with `/api/health` returning `ok` and fake staff sign-in
  returning HTTP 401, then dropped the temporary restore database. Sampled
  restored staging counts were: 13 ledger entries, 142 audit records, 9 devices,
  13 sessions, and 23 update packages;
- the Coolify API token used for this rehearsal was exposed in chat and must be
  rotated before further operational use. The token value is intentionally not
  recorded in repository files.
- PR #40 merged this work into `main` on 2026-05-19 with merge commit
  `962281bb03d28e8e34eb959072a2f6c0b528aa26`. Remote `PR Verification Result`
  passed for head commit `784a0df56c7cbf05d07810c11a0925998bb9da2e` in
  workflow run `26114016147`.

Coolify staging deploy automation branch verification on 2026-05-20:

- branch `codex/coolify-staging-deploy-workflow` adds
  `.github/workflows/coolify-staging-deploy.yml` so Platform API/Coolify-related
  `main` pushes queue a Coolify deployment through the official Coolify API,
  poll the returned deployment UUID, and verify
  `AFK4_STAGING_PLATFORM_BASE_URL/api/health`;
- the workflow requires GitHub repository variables `COOLIFY_BASE_URL`,
  `COOLIFY_STAGING_APP_UUID`, and `AFK4_STAGING_PLATFORM_BASE_URL`, plus the
  repository secret `COOLIFY_API_TOKEN`;
- GitHub repository variables `COOLIFY_BASE_URL=https://cool.mubi.dev`,
  `COOLIFY_STAGING_APP_UUID=d3fm17hl6kb7sossg1kj8buq`, and
  `AFK4_STAGING_PLATFORM_BASE_URL=https://afk4.staging.mubi.dev` are
  configured. Repository secret `COOLIFY_API_TOKEN` is configured; the token
  value is intentionally not recorded in repository files;
- EF migration file changes fail closed on automatic deploy. After a backup and
  explicit staging database migration, the workflow can be manually dispatched
  with `confirm_migrations_applied=true`;
- `docs/operations/coolify-staging-deploy.md` now documents the automated
  deploy path, required GitHub configuration, and migration guard.
- PR #43, `Add Coolify staging deploy workflow`, merged into `main` on
  2026-05-20 with merge commit `8f9af8ee04a83213542e201cf38f7db2c09ff432`.
  The PR head was `013241aca65c4bf74a4359e2ada48188924d830c`, and remote
  `PR Verification Result` passed in workflow run `26143505334`.
- the first post-merge `Coolify Staging Deploy` workflow on `main` passed in
  workflow run `26144123936`. It queued Coolify deployment
  `ixeoc17m6hsyimfb3zop0ca2`, observed status `finished`, and verified
  `https://afk4.staging.mubi.dev/api/health` with `status = ok`.

Operator App redesign branch verification on 2026-05-20:

- branch `codex/operator-app-redesign` continues PR #44 and adds a
  Russian-first primary operator UI for sign-in, floor map seat actions, POS,
  players, and shifts;
- the accepted Operator App visual/workflow target was recorded in
  `docs/product/operator-app-ui-target.md` so future UI work has a durable
  reference beyond chat or local design artifacts;
- Operator App currency is configurable through
  `AFK4_OPERATOR_CURRENCY_CODE`, defaults to `TJS`, and is passed into POS,
  player wallet/debt, and shift money commands;
- the WPF floor-map shell now has a visual alignment pass toward the accepted
  `afk4-operator-ui-concept.png` direction: fixed dark top command bar,
  selected-state left rail, rounded button/chip chrome, metrics and filter
  chips above the dense seat map, real zone chips, tone-colored seat badges,
  tone-colored operational signals, and a right selected-seat action panel;
- the floor map now maps seat states to the documented operator status tones:
  ready, active, pending, warning, blocking, offline, and service. The floor
  summary includes pending and problem counts, active sessions stay active when
  realtime device status changes arrive, and offline active PCs are surfaced as
  warning/problem seats instead of being collapsed into free/offline. The
  selected-seat quick start action is available only for ready seats, not
  offline/service/problem seats;
- the selected-seat panel now separates authoritative backend confirmation
  from Agent/device command status so operators can distinguish "backend
  accepted" from "device command sent / not required / not sent". The active
  session progress bar now binds to session remaining time instead of a static
  placeholder value;
- local Operator App tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 177 passed, 0 failed, 0 skipped.
- full local solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 751 passed, 0 failed, 0 skipped.

Operator App runtime decision update on 2026-05-20:

- after launching and reviewing the WPF redesign branch, the Operator App UI
  runtime decision changed from WPF + MVVM to a native .NET Windows desktop
  shell with WebView2 and React/TypeScript UI;
- PRD, platform architecture, client packaging design, UI target, roadmap, and
  this progress snapshot were updated to record the new source of truth;
- the migration plan is
  `docs/superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md`;
- at the runtime decision point, no implementation cutover had been completed
  yet. The current WPF code should be treated as parity reference and
  temporary legacy code until the WebView2 React app can cover the pilot
  operator day flow;
- full local solution tests were re-run after the decision documentation and
  WPF progress-binding crash fix:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 751 passed, 0 failed, 0 skipped.

Operator App WebView2/React first implementation on 2026-05-20:

- `src/AFK4.Operator.App` now starts `Web/WebViewOperatorWindow.xaml` instead
  of the legacy WPF `MainWindow.xaml`. The new startup window initializes
  WebView2, maps local frontend assets through a virtual host, injects typed
  operator config into `window.__AFK4_OPERATOR_CONFIG__`, and preserves the app
  title/minimum desktop size/runtime environment behavior.
- `src/AFK4.Operator.App.Web` was added with Vite, React, TypeScript,
  `lucide-react`, Vitest, Testing Library, and production `dist` build output.
  The WebView2 host prefers the built Vite `dist` during local repo runs and
  falls back to copied `WebAssets/index.html` when packaged assets are the only
  available frontend surface.
- The React first screen implements the accepted operator-console direction
  with dark top command bar, dark left rail, dense floor map, status metrics,
  filters, operational signals, and right selected-seat panel. It maps ready,
  active, pending, warning, blocking, offline, and service tones in the UI, but
  still uses local fixture state. Auth, protected token bridge, backend API
  clients, SignalR, POS, players, shifts, Pilot Setup, diagnostics, updates,
  audit, and reports still need to be ported before WPF legacy code can be
  removed.
- The first visual refinement removed the native Windows title bar, added
  React-rendered window controls backed by a narrow WebView2 host message
  bridge, and made the floor-map screen smaller/denser. A later same-day
  pass aligned the fixture screen more closely to public SmartShell map
  references: dark map canvas, 74 px left rail, larger readable host tiles,
  color accents, critical-state pills instead of bright metric cards, dark
  selected-PC detail panel, and 24 local fixture hosts for density review.
- The React fixture app now also includes SmartShell-inspired secondary
  workspaces for Dashboard, Booking, POS/Shop, Clients, Payments, Logs, and
  Settings. These screens are visual/workflow fixtures only; backend auth,
  protected token bridge, typed API clients, SignalR, permissions, and
  backend-confirmed critical actions still need to be wired before this can
  replace WPF parity for pilot operations.
- On 2026-05-21, the Dashboard fixture received a focused design pass so it
  matches the dense floor-map direction more closely without trying to show the
  whole product at once. The current Dashboard uses a four-column/four-panel
  grid: the primary operator focus and secondary signal queue span the first
  three columns, while `Управление` and `Пульс смены` are separate top-right and
  bottom-right panels. The management panel has four big-number quick
  transitions, and the shift pulse panel uses circular donut charts for cash
  against daily target, active PCs out of total PCs, attention items out of
  monitored PCs, and bookings out of available slots. The Dashboard period
  control now uses common `Сегодня`, `Неделя`, and `Месяц` presets plus a
  manual date range; the same active range drives the fixture KPI values and
  the export action label. A final polish pass separated export from the period
  presets, added a compact range-duration label, fixed Russian KPI plural
  forms, and added hover/focus states for quick transition cards. Dashboard
  headings and subtitles now use
  operator-facing Russian copy instead of raw backend/device strings such as
  `Lock command failed`. The separate Dashboard side panel was removed so the
  screen can breathe and fit the 1280x720 minimum viewport without horizontal or
  vertical page overflow. This note records the design baseline; the Dashboard
  was later wired to backend summary data in the same branch.
- The floor-map fixture received a matching polish pass on 2026-05-21 without
  changing its role as the primary live workspace. It now uses Russian
  operator-facing state-strip labels, separates `Техрежим` from map/table/booking
  view switches, makes host tiles keyboard-focusable with hover/focus feedback,
  and keeps the selected-PC side panel production-dense with a compact status
  row before fast actions and session/device/billing details. The selected-PC panel now maps fixture
  values such as `Wallet`, `Lease fresh`, and `Online · unlocked` into operator
  copy such as `Депозит`, `Сессия подтверждена`, and
  `Онлайн · разблокирован`; critical-action copy references platform
  confirmation instead of raw backend wording.
- The booking fixture received its first operator-flow design pass on
  2026-05-21. The screen is now full-width, without the generic right summary
  panel, and uses a four-column grid split into `Лента броней`,
  `Выбранная бронь`, `Онлайн-заявки`, and `Новая бронь`. The fixture copy now
  targets real operator work: today's landing list, selected booking actions
  (`Открыть карту`, `Посадить`, `Перенести`, `Отменить`), online request
  handling, and quick booking creation. This is still fixture-backed design
  work; backend booking contracts, conflict validation, permissions, and
  realtime updates remain to be wired.
- The POS fixture received its first operator-flow design pass on 2026-05-21.
  The screen is now full-width, without the generic right summary panel, and
  uses a four-column cashier layout: product/service catalog, current cart,
  payment confirmation, recent receipts, and quick cashier operations. The copy
  is now operator-facing Russian for sale, refund, stock, receipt, cash,
  deposit, customer, and shift surfaces. This remains fixture-backed design
  work; backend POS contracts, inventory writes, refund/void rules, payment
  idempotency, receipt state, permissions, and realtime shift totals remain to
  be wired.
- The Clients fixture received its first operator-flow design pass on
  2026-05-21. The screen is now full-width, without the generic right summary
  panel, and uses a four-column customer-service layout: searchable client
  list, selected client card, deposit/debt/discount/visit metrics, quick client
  operations, segments, and recent client history. The copy is now
  operator-facing Russian for client search, online clients, deposits, debts,
  VIP state, booking context, and client-card actions. This remains
  fixture-backed design work; backend client search, account details, wallet
  ledger, debt settlement, discount rules, privacy/audit constraints, and
  realtime session linkage remain to be wired.
- The Payments fixture received its first operator-flow design pass on
  2026-05-21. The screen is now full-width, without the generic right summary
  panel, and uses a four-column cashier-settlement layout: shift operation
  ledger, shift totals, cash reconciliation, payment method breakdown, cash
  movement log, and report/export actions. The copy is now operator-facing
  Russian for revenue, cash, card, deposits, refunds, reconciliation, shift
  journal, cash report, CSV export, and discrepancies. This remains
  fixture-backed design work; backend immutable ledger reads, cash drawer
  movements, refund/void policy, reconciliation workflow, report exports,
  permissions, audit trail, and shift-close confirmation remain to be wired.
- The Logs fixture received its first operator-flow design pass on 2026-05-21.
  The screen is now full-width, without the generic right summary panel, and
  uses a four-column investigation layout: event journal, selected event
  details, filters, shift audit, event sources, and export actions. The copy is
  now operator-facing Russian for shift events, errors, commands, cash events,
  audit, source filtering, selected event context, and support exports. This
  remains fixture-backed design work; backend audit/event search, retention
  policy, permission filtering, correlation IDs, export generation, and support
  handoff flows remain to be wired.
- The Settings fixture was revised on 2026-05-21 from a technician-oriented
  `Ops` screen into an owner/admin-facing settings screen. The rail label is now
  `Настройки`; the screen remains full-width, without the generic right summary
  panel, but no longer follows the dense operations grid. It now uses a simpler
  settings layout with section navigation, club profile fields, room/workstation
  setup, tariffs, club readiness, and common admin actions such as adding a PC,
  creating a tariff, inviting staff, and checking devices. A same-day polish pass
  removed the shared top status strip and period-style tabs from Settings because
  they duplicated the left settings navigation and did not fit this screen's
  configuration task. This remains fixture-backed design work; backend club settings, room/workstation
  management, tariff rules, staff/role management, device setup, and validation
  flows remain to be wired.
- A same-day header-control audit removed unwired top pseudo-tabs from Map,
  Booking, POS, Clients, Payments, Logs, and Settings. Summary strips remain on
  the operational screens where they provide quick context. The only remaining
  segmented top control is the Dashboard period/date range because it changes
  the displayed fixture metrics and export label; Map and Booking keep only
  direct header actions (`Техрежим` and `Создать`).
- A same-day fixture interaction and motion pass added local stateful behavior
  across the React Operator UI: selected floor-map seat state, Dashboard
  period/focus/card interactions, Booking selection/action feedback, POS
  category/search/cart/payment behavior, Clients segment/client/action
  behavior, Payments ledger/method/export behavior, Logs filter/source/export
  behavior, and Settings section/profile-save behavior. The visual layer now
  includes restrained enter/hover/focus transitions, attention pulse,
  animated numeric counters, donut-chart value transitions, global feedback
  notices, and `prefers-reduced-motion` handling. This remains fixture-backed
  UI polish; critical actions still need real backend confirmation after API
  clients and SignalR are wired.
- The WebView2/React Operator App design phase is closed for the current
  branch as of 2026-05-21. Map, Dashboard, Booking, POS, Clients, Payments,
  Logs, and Settings should now be treated as the accepted fixture design
  baseline. Continue with backend API/SignalR and parity wiring; reopen
  visual design only for concrete defects found during real-data or staging
  smoke, not for another broad fixture-only polish pass.
- The next-session engineering kickoff started on 2026-05-21 with the
  auth/token boundary. The WebView2 host now routes `auth:*` messages to a
  test-covered native bridge backed by the existing staff auth API client and
  protected token store. React has an auth client, common error projection,
  staff sign-in gate, sign-out action, and no browser token persistence. The
  accepted fixture screens remain behind a restored or newly signed-in session;
  typed API clients beyond auth, SignalR realtime state, and backend-confirmed
  floor-map actions remained the next implementation work at that checkpoint.
- The next engineering step in the same 2026-05-21 session added typed
  frontend API client boundaries beyond auth: `PlatformApiClient` handles
  bearer-token requests, JSON bodies, optional 404/204 responses, CSV text
  reads, and Platform API error projection; `operatorApiClients` maps current
  backend routes for floor map, sessions, POS, players, shifts/reports,
  settings/pilot setup, devices, diagnostics, updates, and audit. These clients
  are route-tested but not yet wired into screen state.
- The following 2026-05-21 engineering step wired the primary React floor map
  to backend data and SignalR context: `floorMapState` maps backend
  `FloorMapDto` seats into the accepted `SeatSummary` tones, `PlatformApiClient`
  loads `/api/branches/{branchId}/floor-map` after native staff session
  restore/sign-in, `operatorRealtime` connects to `/hubs/devices` with the
  native access token, and `deviceStatusChanged` updates are applied by
  `deviceId` or machine name. The screen keeps the existing fixture state as a
  browser-dev/no-backend fallback and does not treat realtime as command
  success.
- local frontend tests passed:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  ```

  Result: 31 passed, 0 failed, 0 skipped.
- local frontend production build passed:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' run build
  ```

  Result: Vite built `dist/index.html`, CSS, and JS assets.
- full local solution tests passed:

  ```powershell
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  ```

  Result: 763 passed, 0 failed, 0 skipped.
- local UI smoke passed through the in-app browser against the Vite preview on
  `http://127.0.0.1:4174/`: title `AFK4 Operator`, the WebView auth entry
  screen rendered with heading `Вход оператора`, password field, sign-in
  button, platform URL, custom window controls, no browser console errors, and
  no horizontal or vertical page overflow. Because this smoke runs outside
  WebView2, the page correctly reported the native host bridge as unavailable.
- Dashboard design-pass verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  ```

  Result: frontend tests passed 7/7, Vite production build passed, and Browser
  smoke against `http://127.0.0.1:4174/` confirmed the simplified Dashboard
  heading, four-column Dashboard grid, four separate panels, content-sized
  primary focus card, stable secondary queue header, four big-number
  management cards, four 90 px donut-chart pulse cards, readable
  numerator/denominator labels, operator-facing section subtitles, the
  `Сегодня`/`Неделя`/`Месяц` period controls, manual date range handoff to
  fixture KPI values/export label, compact range-duration label, Russian KPI
  plural forms, quick transition hover/focus states, removal of the Dashboard
  side panel, and no
  horizontal or vertical page overflow at 1280x720. The desktop
  `AFK4.Operator.App.exe` was relaunched locally against the new React build.
- Map polish verification on 2026-05-21 reused the same frontend test and Vite
  build commands. Browser smoke against `http://127.0.0.1:4174/` confirmed the
  Russian state-strip labels, separated `Техрежим` action, compact selected-PC
  status row, translated billing/device/command labels, hover/focus-capable
  host tiles and quick actions, no visible raw `backend` copy on the map screen,
  and no horizontal or vertical page overflow.
- Booking design-pass verification on 2026-05-21 reused the same frontend test
  and Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the full-width booking screen, four-panel booking grid, no generic
  summary side panel, expected booking filters and actions, four timeline
  booking cards, two online request cards, and no horizontal or vertical page
  overflow at 1280x720.
- POS design-pass verification on 2026-05-21 reused the same frontend test and
  Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the full-width POS screen, five POS panels, eight product cards,
  four quick operation cards, three recent receipts, no generic summary side
  panel, readable quick operations, fitting cart/catalog content, and no
  horizontal or vertical page overflow at 1280x720.
- Clients design-pass verification on 2026-05-21 reused the same frontend test
  and Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the full-width Clients screen, five clients panels, five client
  rows, four quick operation cards, four segment cards, three history rows, no
  generic summary side panel, fitting list/actions/segments/history content,
  and no horizontal or vertical page overflow at 1280x720.
- Payments design-pass verification on 2026-05-21 reused the same frontend test
  and Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the full-width Payments screen, six payment panels, five operation
  rows, four payment method cards, three cash movement rows, four report/export
  actions, no generic summary side panel, fitting ledger/summary/reconcile/
  methods/cash/export content, and no horizontal or vertical page overflow at
  1280x720.
- Logs design-pass verification on 2026-05-21 reused the same frontend test and
  Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the full-width Logs screen, six log panels, five event rows, three
  audit rows, four source cards, six filter actions, four export actions, no
  generic summary side panel, fitting journal/detail/filter/audit/source/export
  content, and no horizontal or vertical page overflow at 1280x720.
- Settings design-pass verification on 2026-05-21 reused the same frontend test and
  Vite build commands. Browser smoke against `http://127.0.0.1:4174/`
  confirmed the `Настройки` rail label, full-width Settings screen, section
  navigation, four profile fields, four room cards, four tariff rows, five
  readiness rows, four admin action cards, no visible `Ops` heading, no generic
  summary side panel, no Settings top status strip or period-style tabs, fitting
  navigation/main/side content, and no horizontal or vertical page overflow at
  1280x720.
- Header-control verification on 2026-05-21 reused the frontend test and Vite
  build commands. Browser smoke across Map, Dashboard, Booking, POS, Clients,
  Payments, Logs, and Settings confirmed no top pseudo-tabs outside Dashboard's
  functional period/date range control; Map and Booking headers now expose only
  `Техрежим` and `Создать`, respectively, and POS/Clients/Payments/Logs/Settings
  have no header tab rows. Operational summary strips remain present on Map,
  Booking, POS, Clients, Payments, and Logs.
- Fixture interaction/motion verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: frontend tests passed 7/7, Vite production build passed, and
  whitespace check was clean apart from expected CRLF conversion warnings.
  Browser smoke against `http://127.0.0.1:4174/` confirmed stateful selected
  seat/action feedback on Map; Dashboard period/focus/donut behavior; Booking
  selection/action feedback; POS add-to-cart, payment-method selection, and
  payment feedback; Clients segment filtering, client selection, and action
  feedback; Payments operation/method/export behavior; Logs filter/detail/
  source/export behavior; Settings section switching and profile-save
  feedback. A follow-up layout check across Map, Dashboard, Booking, POS,
  Clients, Payments, Logs, and Settings reported zero horizontal overflow for
  document, body, shell, and main workspaces.
- Auth/token boundary verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  & 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: frontend tests passed 15/15, Vite production build passed, focused
  Operator App tests passed 189/189, full solution tests passed 763/763, and
  whitespace check was clean apart from expected CRLF conversion warnings.
  Browser smoke against `http://127.0.0.1:4174/` confirmed the WebView auth
  entry screen rendered with title `AFK4 Operator`, heading `Вход оператора`,
  password field, sign-in button, custom window controls, and no horizontal or
  vertical page overflow. The browser smoke runs outside WebView2, so it
  correctly reported the native host bridge as unavailable rather than
  persisting tokens in browser storage.
- Selected-seat action verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: frontend tests passed 34/34, Vite production build passed, and full
  solution tests passed 763/763. Browser smoke against
  `http://127.0.0.1:4175/` confirmed the WebView auth entry screen rendered
  with no console errors outside WebView2. The desktop `AFK4.Operator.App.exe`
  was relaunched locally with `AFK4_OPERATOR_PLATFORM_BASE_URL` pointing at
  `https://afk4.staging.mubi.dev` and `AFK4_OPERATOR_CURRENCY_CODE=TJS`.
  Whitespace check was clean apart from expected CRLF conversion warnings.
- Remaining workspace backend-wiring verification on 2026-05-21:

  ```powershell
  & 'C:\Program Files\nodejs\npm.cmd' test
  & 'C:\Program Files\nodejs\npm.cmd' run build
  & 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
  & 'C:\Program Files\Git\cmd\git.exe' diff --check
  ```

  Result: frontend tests passed 34/34, Vite production build passed, full
  solution tests passed 763/763, and whitespace check was clean apart from
  expected CRLF conversion warnings. The route-level frontend tests now include
  tariff/package option reads, and the App tests verify POS sale/manual payment
  confirmation through backend calls.

## Historical Reference

Long phase-by-phase notes, earlier test output, and old smoke evidence were
archived to keep new session context small:

- `docs/archive/progress/2026-05-12-vertical-slice-progress-history.md`
