# AFK4 Vertical Slice Progress

Status: realtime device channel implemented on branch `feature/realtime-device-channel`
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

## Latest Verified State

Full verification was run from
`D:\afk4.net\.worktrees\realtime-device-channel` on 2026-05-12:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' restore AFK4.sln
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Results:

- restore completed with all projects up to date;
- build succeeded with 0 warnings and 0 errors;
- tests passed with 33 visible passing tests, 0 failed, 0 skipped.

Live smoke was run on `http://localhost:5074`:

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

1. Add device enrollment.
2. Add device credentials.
3. Add command status persistence.
4. Sequence identity, tenancy, RBAC, and device management according to the
   PRD and architecture after the device identity and command persistence
   foundations are in place.
