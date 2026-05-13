# AFK4 Vertical Slice Progress

Status: Phase 5 billing, immutable ledger, tariffs, and packages foundation is
implemented on `codex/phase5-billing-ledger-tariffs-packages` after Phase 4
session lifecycle and grace-mode foundation was merged to `main`
Last updated: 2026-05-13

## Scope

This document tracks delivery progress and known implementation deviations for
the first technical vertical slice and the realtime device channel follow-up.
It is intentionally separate from `AGENTS.md` because progress changes more
frequently than agent instructions.

Stable product and architecture decisions live in:

- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`

The implementation plans for this slice live in:

- `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`
- `docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`
- `docs/superpowers/plans/2026-05-12-afk4-phase2-identity-tenancy-rbac-audit.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase3-club-layout-device-management.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase4-session-lifecycle-grace-mode.md`
- `docs/superpowers/plans/2026-05-13-afk4-phase5-billing-ledger-tariffs-packages.md`

## Phase 5 Billing, Ledger, Tariffs, And Packages

Started on `codex/phase5-billing-ledger-tariffs-packages` after `main` commit
`c0e7927`:

- Added the focused Phase 5 implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase5-billing-ledger-tariffs-packages.md`.
- Added shared contracts for:
  - player account creation;
  - wallet summaries, top-ups, refunds, debt payments, and manual ledger
    corrections;
  - tariff creation, versioning, and calculation;
  - package definitions, package purchases, and player package projections;
  - session billing modes on start/extend requests.
- Added EF Core billing persistence and migration
  `AddBillingLedgerTariffsPackages` for:
  - `player_accounts`;
  - `ledger_entries`;
  - `billing_command_idempotency`;
  - `tariffs`;
  - `tariff_versions`;
  - `package_definitions`;
  - `player_packages`.
- Kept balances derived from immutable `ledger_entries`; no mutable wallet,
  debt, or package balance columns were added.
- Added ledger projection for wallet/debt and package remaining seconds.
- Added idempotent billing command handling for player creation, wallet
  top-ups, refunds, debt payments, manual corrections, tariff changes, and
  package purchase flows.
- Added tariff versioning and calculation foundation with billable-minute
  rounding.
- Added package definition, purchase, and ledger-backed time consumption
  foundation.
- Added protected backend endpoints for player creation, billing summaries,
  wallet/debt/refund/manual correction flows, tariff management/calculation,
  package management/purchase, and player package reads.
- Integrated session start/extend with prepaid wallet, postpaid debt, and
  package-backed billing modes.
- Added authenticated HTTP fallback for device command results returned through
  heartbeat polling when the realtime hub is unavailable.

Latest Phase 5 verification:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddBillingLedgerTariffsPackages --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "BillingContractSerializationTests|TariffContractSerializationTests|PackageContractSerializationTests|SessionContractSerializationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "LedgerBalanceProjectorTests|EfBillingCommandServiceTests|EfTariffServiceTests|EfPackageServiceTests|BillingEndpointTests|EfSessionBillingIntegrationTests|EfSessionCommandServiceTests|SessionEndpointTests|DeviceHeartbeatServicePersistenceTests|DeviceCommandEndpointTests|DeviceCommandDispatchServiceTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- local `dotnet-ef` 10.0.4 restored successfully;
- EF migration generation completed after a successful design-time build;
- targeted shared contract tests passed 9/9;
- targeted Platform API Phase 5/session/device fallback tests passed 137/137;
- Agent Service tests passed 25/25;
- full solution build succeeded with 0 warnings and 0 errors;
- full solution tests passed 268/268;
- migration sanity confirmed the seven Phase 5 tables and expected indexes for
  player accounts, ledger chronology, session/package ledger lookups, billing
  idempotency, tariff versions, tariffs, package definitions, and player
  package purchases;
- forbidden schema field grep found no `wallet_balance`, `debt_balance`,
  `remaining_seconds`, or `remaining_bonus_seconds` columns in generated
  migrations.

Phase 5 local PostgreSQL live smoke has not been run yet. The runbook at
`docs/operations/local-postgres-smoke.md` now includes the Phase 5 smoke path
for wallet top-up, manual correction, tariff calculation, prepaid session
idempotency, package purchase/consumption, postpaid debt, debt payment, refund,
and direct SQL inspection.

Known Phase 5 limitations:

- Operator production UX is not implemented in this phase.
- POS, inventory, shifts, and receipts remain out of this phase.
- Agent enforcement and Player Shell billing UI remain out of this phase.
- Phase 4 still leaves ended sessions in `ending` until a later Agent
  acknowledgement/completion workflow; run billing-mode session smoke starts on
  fresh or separate assigned seats.

## Implemented Foundation

- Repository baseline with `global.json`, `Directory.Build.props`,
  `.editorconfig`, `.gitignore`, and expanded `README.md`.
- `AFK4.sln` with backend, shared contracts, building blocks, Agent Service,
  Operator App, Player Shell, and tests.
- Strongly typed Guid ID primitives in `AFK4.BuildingBlocks`.
- Shared DTO contracts for device heartbeat and floor map.
- Platform API endpoints:
  - `GET /api/health`
  - `GET /api/branches/{branchId}/floor-map`
  - `POST /api/devices/{deviceId}/heartbeat`
- SignalR hub at `/hubs/devices`.
- Server broadcast event `deviceStatusChanged`.
- Agent Service options, heartbeat payload factory, and HTTP heartbeat worker
  loop.
- WPF Operator App shell with floor map ViewModel.
- WPF Player Shell fullscreen locked-state skeleton.
- Master MVP PRD at `docs/product/AFK4-MVP-PRD.md`.

## Realtime Device Channel

Implemented on branch `feature/realtime-device-channel`:

- Shared realtime contracts and stable event/method names for device
  connection, command dispatch, and command result acknowledgement.
- Backend device hub registration and `POST /api/devices/{deviceId}/commands`
  dispatch endpoint with basic command request validation.
- Agent SignalR client, command acknowledgements, reconnect re-registration,
  and HTTP heartbeat compatibility when realtime startup fails.
- Operator realtime status state path with dispatcher-safe ViewModel updates
  and startup failure handling.

## Device Enrollment, Credentials, And Command Status

Implemented on `main` after the realtime device channel merge:

- Shared contracts for enrollment code creation, device enrollment responses,
  device credential headers, and command status responses.
- Backend in-memory enrollment code flow:
  - `POST /api/branches/{branchId}/device-enrollment-codes`
  - `POST /api/devices/enroll`
- Enrollment-issued device credentials with server-side hashed secret storage.
- Heartbeat credential validation through `X-AFK4-Device-Credential`.
- SignalR device registration credential validation through
  `DeviceConnectionRequest.CredentialSecret`.
- Backend in-memory command status tracking:
  - commands are stored as `Pending` when dispatched;
  - reported command results update the stored status;
  - `GET /api/devices/{deviceId}/commands/{commandId}/status` returns current
    status.
- Agent options and HTTP heartbeat now carry the enrollment-issued device
  credential secret.

## PostgreSQL-Backed Device Persistence

Implemented on `main` after the in-memory device management foundation:

- Added `PlatformDbContext` with EF Core/Npgsql configuration.
- Added initial migration `AddDevicePersistence` for:
  - `devices`
  - `device_credentials`
  - `device_enrollment_codes`
  - `device_commands`
- Replaced runtime device enrollment and credential validation with
  `EfDeviceEnrollmentService`.
- Replaced runtime command status storage with `EfDeviceCommandStore`.
- Replaced heartbeat handling with `DeviceHeartbeatService`, which persists the
  latest device heartbeat state before broadcasting realtime status.
- Added API test host override that uses EF InMemory provider so automated tests
  do not require a local PostgreSQL server.

## Local PostgreSQL Runbook And Smoke

Implemented on `codex/local-postgres-smoke-runbook` after commit `69a7f4c`:

- Added root `compose.yaml` for a localhost-bound PostgreSQL dev database.
- Added `.config/dotnet-tools.json` to pin local `dotnet-ef` at version
  `10.0.4`.
- Added `docs/operations/local-postgres-smoke.md` with commands for:
  - starting PostgreSQL through Docker Compose;
  - restoring the EF CLI tool;
  - applying EF migrations;
  - starting the Platform API;
  - running health, device enrollment, heartbeat, command creation, and command
    status checks.
- Linked the runbook from `README.md`.

## Phase 2 Identity, Tenancy, RBAC, And Audit Baseline

Started on `codex/phase2-identity-tenancy-rbac-audit` after commit `892c3d3`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-12-afk4-phase2-identity-tenancy-rbac-audit.md`.
- Added shared staff sign-in contracts and explicit permission names.
- Added EF Core entities and migration `AddIdentityTenancyAndAudit` for:
  - `organizations`
  - `branches`
  - `staff_users`
  - `staff_role_assignments`
  - `staff_access_tokens`
  - `audit_records`
- Added EF Core entity and migration `AddStaffRefreshTokens` for
  `staff_refresh_tokens`.
- Added predefined MVP role-to-permission mapping.
- Added staff sign-in through `POST /api/auth/staff/sign-in` with opaque
  bearer access tokens stored as hashes.
- Added `POST /api/auth/staff/refresh` with refresh token rotation,
  hash-only refresh token storage, and replay rejection.
- Added request-time staff context resolution from `Authorization: Bearer`.
- Added branch-scoped authorization for
  `POST /api/branches/{branchId}/device-enrollment-codes`.
- Added audit records for allowed and denied device enrollment-code creation.
- Added staff authorization and audit coverage for:
  - `POST /api/devices/{deviceId}/commands` with
    `devices.commands.dispatch`;
  - `GET /api/devices/{deviceId}/commands/{commandId}/status` with
    `devices.commands.status.view`.
- Added shared contracts for credential rotation and revocation responses.
- Added `EfDeviceCredentialLifecycleService`:
  - rotation revokes current active credentials and issues a new hashed-secret
    credential;
  - revocation marks the target credential with `RevokedAtUtc`;
  - existing credential validation rejects revoked credentials.
- Added staff authorization and audit coverage for:
  - `POST /api/devices/{deviceId}/credentials/rotate` with
    `devices.credentials.rotate`;
  - `POST /api/devices/{deviceId}/credentials/{credentialId}/revoke` with
    `devices.credentials.revoke`.
- Added Operator App protected token storage abstraction using Windows DPAPI
  for access and refresh token snapshots.
- Updated API tests so device enrollment and heartbeat setup use an authorized
  technician before creating enrollment codes.
- Added a shared Operator-facing `DispatchDeviceCommandRequest` contract.
- Added Operator App typed device API client coverage for:
  - bearer-authenticated enrollment-code creation;
  - device command dispatch;
  - device command status inspection;
  - credential rotation;
  - credential revocation.
- Added a dense WPF/MVVM technician panel on the existing floor-map shell for
  enrollment, command status, and credential lifecycle workflows.

## Phase 3 Club Layout And Device Management

Started on `codex/phase2-identity-tenancy-rbac-audit` after commit `f072f6d`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase3-club-layout-device-management.md`.
- Expanded the shared floor-map seat contract with persisted layout and device
  attachment fields:
  - `ZoneId`
  - `SortOrder`
  - `DeviceId`
  - `DeviceName`
  - `IsDeviceOnline`
  - `IsDeviceLocked`
  - `LastHeartbeatAtUtc`
  - `AgentVersion`
  - `ShellVersion`
- Added EF Core entities and migration `AddClubLayout` for:
  - `zones`
  - `seats`
  - `device_seat_assignments`
- Added `EfFloorMapReadService`, which builds the branch floor map from
  persisted branch, zone, seat, active device-seat assignment, and latest
  device heartbeat state.
- Replaced the runtime floor-map service registration with the EF-backed read
  service.
- Protected `GET /api/branches/{branchId}/floor-map` with staff bearer
  permission `floor_map.view`.
- Added tests for:
  - floor-map contract serialization with persisted layout/device fields;
  - EF floor-map read service state projection;
  - unauthorized, forbidden, and authorized persisted floor-map endpoint paths.
- Added shared installed app report contracts:
  - `InstalledAppReportRequest`;
  - `InstalledAppDto`.
- Added EF Core entity and migration `AddDeviceInstalledApps` for
  `device_installed_apps`.
- Added device-authenticated
  `POST /api/devices/{deviceId}/installed-apps/report`, which replaces the
  latest installed app snapshot rows for the reporting device.
- Added route/body/credential identity validation for installed apps reports:
  - route `deviceId` must match request `DeviceId`;
  - the supplied device credential must match the request organization, branch,
    and device.
- Added tests for:
  - installed app report contract serialization;
  - successful snapshot replacement;
  - missing credential rejection;
  - route/request device mismatch rejection;
  - credential-for-different-device rejection.
- Added shared device detail contract `DeviceDetailDto`.
- Added permission `devices.detail.view` and mapped it for owner, branch
  manager, shift supervisor, and technician roles.
- Added staff-protected `GET /api/devices/{deviceId}`, returning:
  - device identity and branch;
  - latest heartbeat online/locked state and component versions;
  - current assigned seat and zone;
  - active credential count;
  - recent command statuses;
  - installed app count.
- Added tests for:
  - device detail contract serialization;
  - unauthenticated device detail rejection;
  - branch staff without `devices.detail.view` rejection;
  - authorized technician detail read;
  - unknown device `404`.
- Extended the Operator App typed device API client with
  `GetDeviceDetailAsync`.
- Added technician ViewModel state and command for loading device detail:
  - machine name;
  - assigned seat and zone;
  - online/lock state;
  - Agent/Shell versions;
  - active credential count;
  - installed app count;
  - most recent command summary.
- Added the device detail block to the existing dense WPF technician panel.
- Added Operator tests for:
  - typed client `GET /api/devices/{deviceId}`;
  - ViewModel detail refresh state projection.
- Added Agent-side installed app inventory collection:
  - Windows registry uninstall-key collector for machine and current-user apps;
  - registry entry mapper with stable UTC date parsing;
  - installed app report factory;
  - authenticated HTTP reporter for
    `POST /api/devices/{deviceId}/installed-apps/report`;
  - Worker startup report attempt before the heartbeat loop, with failures
    logged without stopping heartbeat.
- Added Agent tests for:
  - static collector behavior;
  - registry entry mapping and date parsing;
  - installed app report factory;
  - authenticated HTTP reporting;
  - Worker collection/report wiring.
- Extended `docs/operations/local-postgres-smoke.md` with a Phase 3 local
  PostgreSQL smoke path for:
  - seeded zone and seat rows;
  - active device-seat assignment;
  - bearer-authenticated persisted floor-map read;
  - device-authenticated installed apps report;
  - staff-protected device detail read;
  - direct PostgreSQL inspection of layout and installed app snapshot rows.

## Phase 4 Session Lifecycle And Grace Mode Foundation

Started on `codex/phase4-session-lifecycle-grace-mode` after commit `bdbc8fd`:

- Added a focused implementation plan at
  `docs/superpowers/plans/2026-05-13-afk4-phase4-session-lifecycle-grace-mode.md`.
- Added shared session contracts in `AFK4.Shared.Contracts.Sessions` for:
  - session states;
  - session DTOs and command responses;
  - start, extend, transfer, and end requests;
  - signed session leases;
  - device session snapshots;
  - reconciliation responses;
  - canonical lease signing payloads.
- Extended heartbeat and realtime connection contracts with active lease
  snapshot fields:
  - `ActiveSessionId`;
  - `ActiveSessionLeaseExpiresAtUtc`;
  - `ActiveSessionLeaseSequence`.
- Added session permissions and role mappings:
  - owner, branch manager, shift supervisor, and cashier/operator can start,
    extend, transfer, end, and view sessions;
  - accountant/auditor can view sessions;
  - technician does not receive session operator actions by default.
- Added session audit action names for start, extend, transfer, and end.
- Added EF Core session entities and migration `AddSessions` for:
  - `sessions`;
  - `session_events`;
  - `session_leases`;
  - `session_command_idempotency`.
- Added explicit session state-machine tests and implementation.
- Added backend ECDSA P-256 lease signing from configured
  `Sessions:SigningPrivateKeyPem`.
- Added idempotent session command service behavior for:
  - starting guest sessions;
  - extending active or paused sessions;
  - transferring active sessions to another assigned seat/device;
  - ending active or paused sessions into `ending` state.
- Session commands dispatch through the existing device command path:
  - `unlock`;
  - `refresh-session-lease`;
  - `lock`.
- Added staff-protected session endpoints:
  - `POST /api/branches/{branchId}/sessions/start`;
  - `POST /api/sessions/{sessionId}/extend`;
  - `POST /api/sessions/{sessionId}/transfer`;
  - `POST /api/sessions/{sessionId}/end`.
- Added floor-map active session projection with `ActiveSessionId`,
  `RemainingSeconds`, and session-aware seat state.
- Added Agent-side signed lease validation using configured
  `Agent:LeaseSigningPublicKeyPem`.
- Added Agent-side current lease storage and command handling:
  - `unlock` and `refresh-session-lease` require and validate a signed lease;
  - `lock` clears the matching current lease.
- Added Agent heartbeat and reconnect snapshots from the current lease store.
- Added device-authenticated
  `POST /api/devices/{deviceId}/session-reconciliation` with actions:
  - `continue` for matching active cloud/local lease state;
  - `unlock` when the backend has an active session but the Agent has no
    current local lease;
  - `lock` when the local lease is unknown, ended, or the cloud session is
    ending.
- Added Agent `SessionReconciliationReporter` and Worker startup reconciliation
  before installed app reporting and heartbeat loop.
- Extended `docs/operations/local-postgres-smoke.md` with a Phase 4 local
  PostgreSQL smoke path for signed lease configuration, session start
  idempotency, extend, optional transfer, end, reconciliation, and direct
  PostgreSQL inspection of session tables.

## Known Deviations And Adaptations

### Solution Format

`dotnet new sln` on the installed .NET 10 SDK defaulted to `.slnx`. The project
requires `AFK4.sln`, so the solution was created with:

```powershell
dotnet new sln -n AFK4 --format sln
```

### Agent HTTP Package

`Microsoft.Extensions.Http` was added to `AFK4.Agent.Service` because the Worker
uses `AddHttpClient` and `IHttpClientFactory`.

### API Error Shape

The heartbeat route/body `DeviceId` mismatch response currently returns an
object with `Error` and message:

```text
Route deviceId must match request DeviceId.
```

The implementation plan used lowercase `error` and a slightly different
message. This is not blocking for the slice because no client depends on the
error contract yet, but it should be normalized before treating API contracts
as stable.

### Device Management Foundation Scope

The current device enrollment, credential, heartbeat state, and command status
implementation now has PostgreSQL-backed runtime persistence through EF Core.
It is still a foundation slice, not the complete production device management
module.

Known limitations:

- enrollment code creation is now protected by staff identity, branch-scoped
  permission checks, and audit;
- device command dispatch and command status endpoints are now protected by
  staff identity, branch-scoped permission checks, and audit;
- device credential rotation and revocation endpoints are now protected by
  staff identity, branch-scoped permission checks, and audit;
- Operator App now has initial technician workflows for enrollment-code
  creation, command dispatch/status inspection, and credential
  rotation/revocation, backed by staff bearer tokens loaded from protected
  token storage;
- the technician panel is still a focused Phase 2 surface, not a full device
  management/settings area or web admin replacement.

### Phase 2 Baseline Scope

The current Phase 2 work is still a baseline plus the first focused Operator
App technician workflows. It does not yet include:

- Operator App sign-in UI and role-aware navigation;
- staff management workflows;
- custom role editing;
- audit search or reports;
- automatic Agent-side consumption of rotated credentials;
- authorization coverage for every future operator-facing endpoint as it is
  added.

### Phase 3 Baseline Scope

The current Phase 3 work has started with persisted layout and backend floor-map
reads only. It does not yet include:

- Operator App layout management UI for creating or editing zones, seats, or
  device-seat assignments;
- full installed app list read path, if needed beyond the current device detail
  installed app count;

### Phase 4 Baseline Scope

The current Phase 4 work adds the backend-authoritative session lifecycle and
grace-mode foundation only. It intentionally does not include:

- Operator App session action UI;
- Operator sign-in UI or role-aware navigation;
- web admin;
- local club server;
- billing ledger charging, POS integration, or tariff calculation beyond
  preserving `TariffRuleVersionId`;
- real Windows lock/unlock enforcement or Player Shell session UI;
- Agent-side lease creation or renewal.

Known limitations:

- session leases are issued by the backend and validated by Agent, but real
  Windows lock/unlock enforcement remains deferred;
- reconciliation dispatches corrective `unlock`/`lock` commands through the
  existing device command path, but offline local event replay is not yet
  modeled beyond `PendingLocalEventCount`;
- session lifecycle audit is written for staff commands, while reconciliation
  is captured in `session_events`.

## Latest Verified State

Full verification was run from `D:\afk4.net` on 2026-05-13 after the Phase 4
session lifecycle and grace-mode foundation:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 149 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for the Phase 4 session lifecycle and grace-mode
foundation was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter SessionContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "SessionStateMachineTests|SessionLeaseSignerTests|EfSessionCommandServiceTests|SessionEndpointTests|SessionReconciliationEndpointTests|EfFloorMapReadServiceTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "SessionLeaseValidatorTests|SessionCommandHandlerLeaseTests|HeartbeatPayloadFactoryTests|SessionReconciliationReporterTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `SessionContractSerializationTests` passed with 2 visible passing tests;
- targeted Platform API session tests passed with 34 visible passing tests;
- targeted Agent lease/reconciliation tests passed with 12 visible passing
  tests.

The Phase 4 local PostgreSQL smoke path was documented in
`docs/operations/local-postgres-smoke.md`, including signed lease key
configuration, idempotent start/extend/end, optional transfer, reconciliation,
and direct session table inspection.

The Phase 4 local PostgreSQL live smoke was run from `D:\afk4.net` on
2026-05-13 using a temporary PostgreSQL container on `localhost:55432` because
port `5432` was already occupied by another Docker project:

```powershell
docker run --rm -d --name afk4-phase4-smoke-postgres -e POSTGRES_HOST_AUTH_METHOD=trust -e POSTGRES_DB=afk4_dev -p 55432:5432 postgres:17-alpine
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --connection "Host=localhost;Port=55432;Database=afk4_dev;Username=postgres"
& 'C:\Program Files\dotnet\dotnet.exe' run --no-launch-profile --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- EF migrations applied through `20260513064045_AddSessions`.
- Platform API health returned `status = ok`.
- Local branch manager sign-in and refresh returned bearer tokens with session
  permissions.
- Device enrollment-code creation, device enrollment, and device-authenticated
  heartbeat succeeded.
- Seeded zone `Main Hall`, seat `PC-SMOKE-001`, and active device-seat
  assignment were returned by the persisted floor-map endpoint.
- `POST /api/branches/{branchId}/sessions/start` returned an active session
  with a signed lease.
- Repeating start with idempotency key `smoke-start-001` returned the same
  `sessionId`.
- `POST /api/sessions/{sessionId}/extend` returned a refreshed signed lease
  with sequence `2`.
- Active device reconciliation returned action `continue`.
- `POST /api/sessions/{sessionId}/end` moved the session to `ending`.
- Ending-state reconciliation returned action `lock`.
- Direct PostgreSQL inspection confirmed:
  - one `sessions` row in `ending` state;
  - two `session_leases` rows with non-empty signatures;
  - `session-started`, `session-extended`, `device-reconciled`, and
    `session-ending` rows in `session_events`;
  - `start`, `extend`, and `end` rows in `session_command_idempotency`;
  - `unlock`, `refresh-session-lease`, and `lock` rows in `device_commands`;
  - succeeded audit rows for session start, repeated start, extend, and end.
- The API process was stopped after smoke verification.
- The temporary PostgreSQL container was stopped and removed, and the temporary
  lease-signing private key file was deleted.

Full verification was run from `D:\afk4.net` on 2026-05-13 after the Phase 3
local PostgreSQL smoke path update:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 101 visible passing tests, 0 failed, 0 skipped.

The full local PostgreSQL live smoke was run from `D:\afk4.net` on 2026-05-13
for the Phase 3 path on `codex/phase3-installed-apps-reporting`:

```powershell
docker compose down -v
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF migrations applied through `AddDeviceInstalledApps`.
- `GET /api/health` returned status `ok`.
- local technician sign-in and refresh returned non-empty access tokens.
- bearer-authenticated device enrollment-code creation returned
  `AFK4-A70C-BB7607073B40`.
- device enrollment returned device
  `e14e78b8-b69d-4d52-a63e-64af11c6e0d7`.
- authenticated heartbeat returned heartbeat interval `10`.
- seeded zone `Main Hall`, seat `PC-SMOKE-001`, and active device-seat
  assignment were returned by the bearer-authenticated floor-map endpoint with
  state `Locked`.
- device-authenticated installed apps report persisted `Counter-Strike 2` and
  `Discord`.
- staff-protected device detail returned machine `PC-SMOKE-001`,
  installed app count `2`, active credential count `1`, assigned seat
  `PC-SMOKE-001`, and zone `Main Hall`.
- bearer-authenticated command dispatch/status returned command status
  `Pending`.
- credential rotation returned credential
  `af62be84-93fe-4197-9966-aa2552750380`.
- heartbeat with the rotated credential returned heartbeat interval `10`.
- credential revocation returned the rotated credential id.
- Direct PostgreSQL reads confirmed the seeded layout row with online/locked
  device state.
- Direct PostgreSQL reads confirmed installed app snapshot rows for
  `Counter-Strike 2` and `Discord`.
- Direct PostgreSQL reads confirmed succeeded audit rows for enrollment-code
  creation, command dispatch/status, credential rotation, and credential
  revocation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Targeted TDD verification for the Phase 3 Operator device detail workflow was
also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorDeviceApiClientTests|TechnicianDeviceWorkflowViewModelTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `OperatorDeviceApiClientTests` and
  `TechnicianDeviceWorkflowViewModelTests` failed first because the typed
  detail client method and ViewModel detail state did not exist, then passed
  after GREEN;
- targeted Operator tests passed with 12 visible passing tests.

Targeted TDD verification for the Phase 3 Agent installed app inventory
collection was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Agent.Service.Tests/AFK4.Agent.Service.Tests.csproj --filter "InstalledAppInventoryTests|WorkerTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `InstalledAppInventoryTests` failed first because the Agent inventory
  snapshot, report factory, reporter, and registry mapper did not exist, then
  passed after GREEN;
- `WorkerTests` failed first because Worker did not collect/report installed
  apps, then passed after GREEN;
- targeted Agent tests passed with 6 visible passing tests.

Targeted TDD verification for the Phase 3 device detail backend slice was also
run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceDetailContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceDetailEndpointTests --no-restore -p:UseSharedCompilation=false
```

Results:

- `DeviceDetailContractSerializationTests` failed first because
  `DeviceDetailDto` did not exist, then passed after GREEN;
- `DeviceDetailEndpointTests` failed first because `GET /api/devices/{deviceId}`
  returned `404`, then passed after GREEN.

Targeted TDD verification for the Phase 3 installed apps reporting slice was
also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter InstalledAppReportContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter InstalledAppsEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Results:

- `InstalledAppReportContractSerializationTests` failed first because the
  installed app report contracts did not exist, then passed after GREEN;
- `InstalledAppsEndpointTests` failed first because `DeviceInstalledAppEntity`
  and `PlatformDbContext.DeviceInstalledApps` did not exist, then passed after
  GREEN;
- full Platform API tests passed with 44 visible passing tests, 0 failed,
  0 skipped.

Targeted TDD verification for the Phase 3 persisted floor-map slice was also
run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter ContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfFloorMapReadServiceTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FloorMapEndpointTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfFloorMapReadServiceTests|FloorMapEndpointTests" --no-restore -p:UseSharedCompilation=false
```

Results:

- `ContractSerializationTests` failed first because `SeatStatusDto` did not
  expose persisted layout/device attachment fields, then passed after GREEN;
- `EfFloorMapReadServiceTests` failed first because layout entities and the EF
  floor-map service did not exist, then passed after GREEN;
- `FloorMapEndpointTests` failed first because the endpoint was anonymous and
  returned in-memory demo seats, then passed after GREEN;
- targeted Platform API floor-map tests passed with 4 visible passing tests.

Full verification was run from `D:\afk4.net` on 2026-05-12 after the Operator
App technician workflow changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 81 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for the Operator technician workflows was also run
for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DispatchDeviceCommandRequestSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --filter "OperatorDeviceApiClientTests|TechnicianDeviceWorkflowViewModelTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Operator.App.Tests/AFK4.Operator.App.Tests.csproj --no-restore -p:UseSharedCompilation=false
```

Results:

- `DispatchDeviceCommandRequestSerializationTests` failed first because the
  shared command request contract did not exist, then passed after GREEN;
- `OperatorDeviceApiClientTests` and
  `TechnicianDeviceWorkflowViewModelTests` failed first because the Operator
  device client and technician ViewModel did not exist, then passed after
  GREEN;
- full Operator App tests passed with 19 visible passing tests.

Full verification was run from `D:\afk4.net` on 2026-05-12 after the Phase 2
device credential lifecycle changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 70 visible passing tests, 0 failed, 0 skipped.

Targeted TDD verification for device credential lifecycle was also run for:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter DeviceCredentialLifecycleContractSerializationTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfDeviceCredentialLifecycleServiceTests --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter DeviceCredentialLifecycleEndpointTests --no-restore -p:UseSharedCompilation=false
```

Results:

- credential lifecycle contract tests passed after the contract RED/GREEN
  cycle;
- EF credential lifecycle service tests passed after the service RED/GREEN
  cycle;
- credential rotation/revocation endpoint authorization and audit tests passed
  after the endpoint RED/GREEN cycle;
- the full Platform API test project now has 37 visible passing tests.

The local PostgreSQL live smoke was rerun from `D:\afk4.net` on 2026-05-12
after the device credential lifecycle changes:

```powershell
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF database update completed; no new migration was required for credential
  lifecycle because `device_credentials.RevokedAtUtc` already existed.
- `GET /api/health` returned status `ok`.
- local technician sign-in and refresh returned non-empty access tokens.
- bearer-authenticated
  `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-F8B7-973E65CD9C33`.
- `POST /api/devices/enroll` returned device
  `d72f0084-4445-485e-8463-f3c2b4cefdd2`.
- authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- bearer-authenticated `POST /api/devices/{deviceId}/commands` followed by
  command status read returned `Pending`.
- bearer-authenticated
  `POST /api/devices/{deviceId}/credentials/rotate` returned credential
  `ba299c3c-c8c2-4a08-833e-d684ce6eb8a0`.
- heartbeat with the rotated credential returned heartbeat interval `10`.
- bearer-authenticated
  `POST /api/devices/{deviceId}/credentials/{credentialId}/revoke` returned
  the rotated credential id.
- Direct PostgreSQL reads confirmed succeeded audit rows for enrollment-code
  creation, device command dispatch/status, credential rotation, and credential
  revocation.
- Direct PostgreSQL reads confirmed two revoked credential rows for the smoke
  device after rotation plus revocation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

The local PostgreSQL live smoke was rerun from `D:\afk4.net` on 2026-05-12
after the Phase 2 refresh-token and command authorization changes:

```powershell
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- PostgreSQL healthcheck reached `healthy`.
- EF migration `20260512092839_AddStaffRefreshTokens` applied to PostgreSQL.
- `GET /api/health` returned status `ok`.
- local technician sign-in returned non-empty access and refresh tokens.
- `POST /api/auth/staff/refresh` returned a rotated token pair, and replay of
  the old refresh token returned `401`.
- bearer-authenticated
  `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-1D03-44405C36A3C9`.
- `POST /api/devices/enroll` returned device
  `6c70a84f-6246-4fe7-aa7b-e6d2f4d38f87` and credential
  `12f392fd-8eca-448d-a9fc-e9c95651e83c`.
- authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- bearer-authenticated `POST /api/devices/{deviceId}/commands` created command
  `b1d839b7-90c8-4158-8668-88e1488cbe5a`.
- bearer-authenticated
  `GET /api/devices/{deviceId}/commands/{commandId}/status` returned
  `Pending`.
- Direct PostgreSQL reads confirmed audit rows for
  `devices.enrollment_codes.create`, `devices.commands.dispatch`, and
  `devices.commands.status.view`, all with `Succeeded` outcome.
- Direct PostgreSQL reads confirmed the latest smoke device had
  `IsOnline = true`, `IsLocked = true`, and a non-null heartbeat timestamp.
- Direct PostgreSQL reads confirmed the latest smoke command stored
  `Type = lock`, `Status = Pending`, and payload
  `{"reason": "local-postgres-smoke"}`.
- Direct PostgreSQL reads confirmed `staff_refresh_tokens` contained revoked
  rows after refresh rotation.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Local PostgreSQL live smoke was run from `D:\afk4.net` on 2026-05-12 after the
Docker Compose runbook was added:

```powershell
docker compose config
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
docker compose up -d postgres
& 'C:\Program Files\dotnet\dotnet.exe' ef database update --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
& 'C:\Program Files\dotnet\dotnet.exe' run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Live smoke results:

- Docker Compose parsed successfully and PostgreSQL healthcheck reached
  `healthy`.
- EF migration `20260512060601_AddDevicePersistence` applied to PostgreSQL.
- PostgreSQL contained `__EFMigrationsHistory`, `devices`,
  `device_credentials`, `device_enrollment_codes`, and `device_commands`.
- `GET /api/health` returned status `ok`.
- `POST /api/branches/{branchId}/device-enrollment-codes` returned enrollment
  code `AFK4-0E77-3FA32297EE7E`.
- `POST /api/devices/enroll` returned device
  `f7f1066b-94b0-4578-be48-75d5512df551` and credential
  `7cf3a039-0c1b-47cf-a824-4deb0e781c8b`.
- Authenticated `POST /api/devices/{deviceId}/heartbeat` returned heartbeat
  interval `10`.
- `POST /api/devices/{deviceId}/commands` created command
  `d5b7c37a-5c01-4668-a9f8-e05f3211d4ec`.
- `GET /api/devices/{deviceId}/commands/{commandId}/status` returned
  `Pending`.
- Direct PostgreSQL reads confirmed the latest smoke device had
  `IsOnline = true`, `IsLocked = true`, and a non-null heartbeat timestamp.
- Direct PostgreSQL reads confirmed the latest smoke command stored
  `Type = lock`, `Status = Pending`, and payload
  `{"reason": "local-postgres-smoke"}`.
- The API process was stopped after smoke verification.
- PostgreSQL was stopped with `docker compose down`; the named volume was kept.

Previous live smoke was run on `http://localhost:5074` for the realtime device
channel:

- `GET /api/health` returned status `ok`;
- `POST /api/devices/d76eff15-9cf9-4c30-a6d4-c05fd215793f/commands`
  returned `DeviceCommandDto` responses for `lock` commands;
- Agent Service was started with the requested organization, branch, device,
  machine, and platform URL environment variables;
- Agent logs showed realtime connection for
  `d76eff15-9cf9-4c30-a6d4-c05fd215793f`;
- after sending another command, Agent logs showed command acknowledgement as
  `Accepted`;
- backend and Agent smoke processes were stopped after verification.

WPF Operator App live smoke was not run in this environment. The automated
Operator App tests are the current proof for the dispatcher-safe realtime
status state path, startup failure handling, protected token storage, typed
device API client, and technician workflow ViewModel behavior.

## Recent Key Commits

- `bdbc8fd docs: add phase 4 session lifecycle plan`
- `f072f6d docs: add phase 3 club layout plan`
- `69a7f4c feat: persist device state and commands with ef core`
- `a176363 fix: harden operator realtime startup and dispatch`
- `8fe0a2e feat: add operator realtime floor map state`
- `ebcaa61 fix: keep agent heartbeat alive across realtime failures`
- `d329897 feat: add agent realtime device client`
- `0ab297f test: cover device command SignalR dispatch`
- `b35be64 feat: add backend device realtime command dispatch`
- `ab681d5 feat: add realtime device contracts`
- `003ab43 docs: add realtime device channel plan`
- `d176048 merge: integrate vertical slice foundation`
- `57b2763 docs: add mvp product requirements`

## Recommended Next Work

1. Later, add Operator App sign-in UI and role-aware startup so staff can
   acquire protected bearer/refresh token snapshots without manual setup.
