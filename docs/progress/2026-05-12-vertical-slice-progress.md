# AFK4 Vertical Slice Progress

Status: PostgreSQL-backed device persistence foundation implemented on `main`
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
  not implemented yet;
- local live smoke against a real PostgreSQL instance has not been run in this
  update; automated tests use EF InMemory to validate service and endpoint
  behavior.

## Latest Verified State

Full verification was run from `D:\afk4.net` on 2026-05-12 after the
PostgreSQL-backed device persistence changes:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- build succeeded with 0 warnings and 0 errors;
- tests passed with 47 visible passing tests, 0 failed, 0 skipped.

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
4. Add local PostgreSQL runbook or Docker Compose setup and run live smoke
   against a real database.
