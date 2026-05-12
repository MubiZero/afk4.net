# AFK4 Vertical Slice Progress

Status: implemented on branch `feature/vertical-slice`  
Last updated: 2026-05-12

## Scope

This document tracks delivery progress and known implementation deviations for
the first technical vertical slice. It is intentionally separate from
`AGENTS.md` because progress changes more frequently than agent instructions.

Stable product and architecture decisions live in:

- `docs/product/AFK4-MVP-PRD.md`
- `docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md`

The implementation plan for this slice lives in:

- `docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md`

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
- WPF Operator App shell with static floor map ViewModel.
- WPF Player Shell fullscreen locked-state skeleton.
- Master MVP PRD at `docs/product/AFK4-MVP-PRD.md`.

## Known Deviations And Adaptations

### Agent Heartbeat Transport

The current Agent Service sends heartbeat through an HTTP POST loop:

```text
Agent -> POST /api/devices/{deviceId}/heartbeat -> Backend -> SignalR broadcast
```

This matches the current vertical slice implementation plan, where the Worker
uses `PostAsJsonAsync`, but it does not yet satisfy the full architecture goal
that the Agent maintains an outgoing SignalR/WebSocket connection for realtime
command and state flow.

Required follow-up: create a focused plan for Agent SignalR/WebSocket
connection, command channel, and Operator App realtime subscription.

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

### Realtime Device Channel Follow-Up

The next focused plan is
`docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`. It keeps
the HTTP heartbeat for compatibility while adding Agent SignalR registration,
backend command dispatch, Agent command acknowledgements, and Operator App
realtime floor-map status updates.

## Latest Verified State

Verification run after the vertical slice and documentation updates:

```powershell
dotnet build AFK4.sln --no-restore
dotnet test AFK4.sln --no-restore
```

Expected current result:

- build succeeds with 0 warnings and 0 errors;
- test suite succeeds with 14 passing tests.

Live API verification was also run on `http://localhost:5074`:

- health returned `ok`;
- floor map returned `Demo Branch` with 2 seats;
- heartbeat returned interval `10` and 0 commands.

## Recent Key Commits

- `12dfb59 docs: add platform architecture spec`
- `d8ab10e docs: add vertical slice implementation plan`
- `f100fbe docs: add mvp delivery roadmap`
- `d8420af chore: add repository baseline`
- `5beb130 chore: scaffold dotnet solution`
- `052dc3d feat: add domain id primitives`
- `c1accc4 feat: add shared platform contracts`
- `44c2c68 feat: add api health and floor map endpoints`
- `5ab5586 feat: add device heartbeat endpoint`
- `4f80a4c feat: add agent heartbeat skeleton`
- `a621f23 feat: add operator floor map shell`
- `674afda feat: add player shell skeleton`
- `288a049 docs: expand project readme`
- `9920878 docs: document approved architecture sections`
- `57b2763 docs: add mvp product requirements`

## Recommended Next Work

1. Execute `docs/superpowers/plans/2026-05-12-afk4-realtime-device-channel.md`.
2. After realtime device channel verification, create a focused plan for device
   enrollment, device credentials, and command status persistence.
3. Do not jump into billing, POS, updates, identity, or Windows enforcement
   before the realtime device channel and device identity foundations are
   resolved unless explicitly reprioritized.
