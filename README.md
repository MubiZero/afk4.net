# AFK4

AFK4 is a cloud-first SaaS platform for managing Windows-based computer clubs.
It is intended to become a full operator-grade platform in the same product
category as Senet, Langame, and SmartShell, not a lightweight web admin panel or
local-only prototype.

## Product Direction

The core product decisions are fixed for the MVP:

- Cloud-first SaaS platform.
- No local club server.
- No web admin panel in the MVP.
- Native Windows Operator App for club staff.
- Windows 10/11 gaming PCs only in the MVP.
- ASP.NET Core modular monolith backend on .NET 10.
- PostgreSQL as the production source-of-truth database.
- WPF + MVVM for Operator App and Player Shell.
- Windows Agent Service on each gaming PC.
- SignalR/WebSocket realtime channel.
- Grace mode only for already active sessions during connectivity loss.
- Multi-tenant from the start.
- Billing through an immutable ledger, not mutable balance fields.
- POS, inventory, shifts, receipts, audit, and centralized updates are part of
  the full MVP roadmap.

## Runtime Parts

AFK4 is split into four main runtime surfaces.

### Platform API

`AFK4.Platform.Api` is the cloud backend. It starts as one ASP.NET Core modular
monolith with strict module boundaries. The backend is the authority for
sessions, billing, POS, roles, audit, device commands, update rollout, and
reconciliation.

The current vertical slice exposes:

- `GET /api/health`
- `GET /api/branches/{branchId}/floor-map`
- `POST /api/devices/{deviceId}/heartbeat`
- SignalR hub at `/hubs/devices`
- SignalR client event `deviceStatusChanged`

### Operator App

`AFK4.Operator.App` is the native Windows application for operators, cashiers,
managers, technicians, accountants, and owners depending on permissions.

The main working screen is the floor map. The current shell shows static seat
cards and establishes the MVVM structure that later connects to backend state,
SignalR updates, session actions, POS, players, shifts, and settings.

### Agent Service

`AFK4.Agent.Service` is the Windows service skeleton for gaming PCs. It creates
heartbeat payloads and posts them to the backend. Later slices add enrollment,
device credentials, lock/unlock enforcement, process policy, session lease
validation, reconnect reconciliation, watchdog behavior, and updates.

### Player Shell

`AFK4.Player.Shell` is the player-facing WPF shell shown on gaming PCs. It is a
UI process, not a trusted business authority. The current shell is a fullscreen
locked-state placeholder.

## Repository Layout

```text
src/
  AFK4.BuildingBlocks/       Shared low-level primitives, such as strong IDs.
  AFK4.Shared.Contracts/     DTOs shared by backend, Agent, Operator, and Shell.
  AFK4.Platform.Api/         ASP.NET Core platform backend.
  AFK4.Agent.Service/        Windows Agent Service skeleton.
  AFK4.Operator.App/         WPF Operator App shell.
  AFK4.Player.Shell/         WPF Player Shell skeleton.

tests/
  AFK4.BuildingBlocks.Tests/
  AFK4.Shared.Contracts.Tests/
  AFK4.Platform.Api.Tests/
  AFK4.Agent.Service.Tests/
  AFK4.Operator.App.Tests/

docs/superpowers/specs/
  2026-05-12-afk4-platform-architecture-design.md

docs/superpowers/plans/
  2026-05-12-afk4-platform-vertical-slice.md
```

## Local Requirements

- Windows with PowerShell.
- Git for Windows.
- .NET SDK `10.0.203` or another compatible .NET 10 SDK allowed by
  `global.json` feature-band roll-forward.

Check the SDK:

```powershell
dotnet --list-sdks
```

## Build And Test

From the repository root:

```powershell
dotnet build AFK4.sln
dotnet test AFK4.sln
```

Expected for the current vertical slice:

- build succeeds with `0` warnings and `0` errors;
- test suite passes.

## Run The Current Slice

Start the backend:

```powershell
dotnet run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Verify health:

```powershell
Invoke-RestMethod http://localhost:5074/api/health
```

Verify the demo floor map:

```powershell
Invoke-RestMethod http://localhost:5074/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map
```

Run the Operator App:

```powershell
dotnet run --project src/AFK4.Operator.App/AFK4.Operator.App.csproj
```

Run the Player Shell:

```powershell
dotnet run --project src/AFK4.Player.Shell/AFK4.Player.Shell.csproj
```

Run the Agent Service skeleton:

```powershell
dotnet run --project src/AFK4.Agent.Service/AFK4.Agent.Service.csproj
```

The Agent currently uses placeholder option defaults. Real device enrollment and
validated configuration are intentionally deferred to later slices.

## Current Implementation State

The first vertical slice foundation is implemented:

- repository baseline with pinned .NET SDK and shared build settings;
- .NET solution and project structure;
- strong Guid ID primitives;
- shared device and floor map contracts;
- backend health, floor map, heartbeat, and SignalR foundation;
- Agent heartbeat payload factory and worker loop skeleton;
- Operator App floor map shell;
- Player Shell locked-state skeleton.

Not implemented yet:

- identity, tenancy enforcement, and RBAC;
- PostgreSQL persistence and EF Core migrations;
- device enrollment and credentials;
- real session lifecycle;
- ledger, tariffs, packages, POS, inventory, shifts, receipts;
- audit search and reports;
- Windows lock/unlock enforcement and launcher control;
- signed updates, rollout, rollback, and installers.

## Engineering Rules

- Keep the backend a modular monolith until the product has a real need to split.
- Keep shared transport DTOs in `AFK4.Shared.Contracts`.
- Keep domain primitives in `AFK4.BuildingBlocks`.
- Do not expose internal domain models directly as API contracts.
- Use TDD for domain rules, contracts, endpoints, Agent behavior, and ViewModels.
- Require backend confirmation for critical Operator App actions.
- Treat local caches as UI acceleration only, never as financial or session
  authority.
- Model money through immutable ledger entries.
- Use idempotency for money, POS, session, and device commands.
- Keep Agent and Player Shell separated: the Shell is UI, the Agent enforces.

## Source Of Truth

Start with this README for orientation, then read:

- [Architecture spec](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md)
- [Vertical slice implementation plan](docs/superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md)
- [Agent/session instructions](AGENTS.md)
