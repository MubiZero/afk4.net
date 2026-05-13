# AFK4 Vertical Slice Progress

Status: Phase 3 club layout and device management started after Phase 2
identity, tenancy, RBAC, audit, device credential lifecycle, and Operator
technician workflows on
`codex/phase2-identity-tenancy-rbac-audit`
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
- installed apps reporting;
- device detail read endpoint and Operator detail workflow;
- automatic Agent-side installed app inventory collection;
- live PostgreSQL smoke for persisted floor-map reads.

## Latest Verified State

Full verification was run from `D:\afk4.net` on 2026-05-13 after the Phase 3
persisted layout and floor-map changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 84 visible passing tests, 0 failed, 0 skipped.

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

1. Continue Phase 3 with installed apps reporting from Agent to backend,
   including an authenticated device endpoint and persisted app snapshot rows.
2. Add the staff-protected device detail read endpoint with assigned seat,
   latest heartbeat state, credential summary, recent command statuses, and
   installed app counts.
3. Extend the existing Operator technician panel with a dense device detail
   workflow backed by the new read endpoint.
4. Add a local PostgreSQL smoke path for seeded zones/seats, device-seat
   assignment, and bearer-authenticated floor-map read verification.
5. Later, add Operator App sign-in UI and role-aware startup so staff can
   acquire protected bearer/refresh token snapshots without manual setup.
