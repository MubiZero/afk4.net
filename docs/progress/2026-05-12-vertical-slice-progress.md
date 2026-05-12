# AFK4 Vertical Slice Progress

Status: Local PostgreSQL runbook and live smoke verified on
`codex/local-postgres-smoke-runbook`
Last updated: 2026-05-12

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

- enrollment code creation is not yet protected by staff identity, RBAC, or
  audit because those modules are still deferred;
- device credential revocation and rotation are not implemented yet;
- Operator App technician workflows for enrollment and status inspection are
  not implemented yet.

## Latest Verified State

Full verification was run from `D:\afk4.net` on 2026-05-12 after the local
PostgreSQL runbook and smoke changes:

```powershell
docker compose config
& 'C:\Program Files\dotnet\dotnet.exe' tool restore
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- Docker Compose config parsed successfully;
- `dotnet-ef` version `10.0.4` restored successfully from the local tool
  manifest;
- build succeeded with 0 warnings and 0 errors;
- tests passed with 47 visible passing tests, 0 failed, 0 skipped.

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

WPF Operator App live smoke was not run in this subagent environment. The
automated Operator App tests are the current proof for the dispatcher-safe
realtime status state path and startup failure handling.

## Recent Key Commits

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

1. Add identity, tenancy, RBAC, and audit around enrollment code creation before
   treating enrollment as production-safe.
2. Add credential revocation/rotation and Operator App technician workflows for
   enrollment and command status inspection.
3. Add Agent installer/enrollment bootstrap so gaming PCs can acquire and store
   credentials without manual configuration.
