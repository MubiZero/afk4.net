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

## Approved Architecture Sections

The initial product architecture was approved as nine sections. This README
keeps the short navigation version; the detailed source of truth is the
[architecture spec](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md).

1. **System Overview**
   Cloud Backend, native Operator App, Windows Agent Service, and Player Shell
   are separate runtime surfaces with the backend as the business authority.
   Details: [System Overview](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#system-overview).

2. **Domain Model**
   Organizations, branches, zones, seats, devices, players, guest sessions,
   sessions, ledger, tariffs, packages, POS, shifts, audit, and updates are the
   core business objects. Details: [Domain Model](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#domain-model).

3. **Backend Modules**
   The backend starts as a modular monolith with explicit modules for identity,
   tenancy, club operations, devices, sessions, billing, POS, audit, updates,
   and notifications. Details: [Backend Modules](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#backend-modules).

4. **Operator App Design**
   The Operator App is dense WPF operator software centered on the floor map,
   seat/session context, POS, players, shifts, reports, and settings. Details:
   [Operator App Design](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#operator-app-design).

5. **Agent And Shell Design**
   The gaming PC runtime is split into an elevated Agent Service and a
   player-facing Shell. The Shell is UI only; the Agent enforces cloud-approved
   state. Details: [Agent And Shell Design](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#agent-and-shell-design).

6. **Realtime Protocol And Reliability**
   REST is used for authoritative commands and reads. SignalR is used for
   realtime status, device commands, session events, and notifications. Active
   sessions may continue during temporary connectivity loss through signed
   leases; new sessions and payments require cloud connectivity. Details:
   [Realtime Protocol And Reliability](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#realtime-protocol-and-reliability)
   and [Grace Mode](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#grace-mode).

7. **Data And Transactions**
   PostgreSQL is the source of truth. Data is tenant-aware, EF Core migrations
   are used, modules keep explicit ownership, read models are allowed for maps
   and reports, and Redis may be added later only as cache/coordination.
   Critical rules include immutable ledger entries, explicit POS/payment and
   session states, versioned tariff calculation, audit for critical changes,
   and idempotency keys for payments, POS, and session/device commands.
   Details: [Data And Transactions](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#data-and-transactions).

8. **Deployment And Updates**
   Dev, staging, and production environments are defined separately. The
   backend deploys as one ASP.NET Core service at the start. Operator App,
   Agent Service, and Player Shell updates are centralized, signed, channelled,
   staged, status-tracked, and rollback-capable. Installers and device
   enrollment are part of the platform design. Details: [Deployment And Updates](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#deployment-and-updates).

9. **MVP Scope**
   The first full MVP includes multi-tenancy, WPF Operator App, Windows devices,
   Agent/Shell, SignalR, session lifecycle, guest and registered players, mixed
   billing, immutable ledger, tariffs/packages, POS, shifts, receipts, launcher,
   Windows control, grace mode, audit, centralized updates, and base reports.
   It excludes web admin, local club server, non-Windows agents, kernel driver,
   country-specific fiscal integrations, mobile app, microservices, and
   full-domain event sourcing. Details: [MVP Scope](docs/superpowers/specs/2026-05-12-afk4-platform-architecture-design.md#mvp-scope).

## Runtime Parts

AFK4 is split into four main runtime surfaces.

### Platform API

`AFK4.Platform.Api` is the cloud backend. It starts as one ASP.NET Core modular
monolith with strict module boundaries. The backend is the authority for
sessions, billing, POS, roles, audit, device commands, update rollout, and
reconciliation. Device enrollment, device credentials, device heartbeat state,
device command status, persisted zones/seats, and floor-map reads are backed by
EF Core/Npgsql persistence.

The current vertical slice exposes:

- `GET /api/health`
- `POST /api/auth/staff/sign-in`
- `POST /api/auth/staff/refresh`
- `GET /api/branches/{branchId}/floor-map` with staff bearer token permission
  `floor_map.view`
- `POST /api/branches/{branchId}/device-enrollment-codes` with staff bearer
  token permission `devices.enrollment_codes.create`
- `POST /api/devices/enroll`
- `POST /api/devices/{deviceId}/heartbeat`
- `POST /api/devices/{deviceId}/installed-apps/report` with device credential
  authentication
- `GET /api/devices/{deviceId}` with staff bearer token permission
  `devices.detail.view`
- `POST /api/devices/{deviceId}/commands` with staff bearer token permission
  `devices.commands.dispatch`
- `GET /api/devices/{deviceId}/commands/{commandId}/status` with staff bearer
  token permission `devices.commands.status.view`
- `POST /api/devices/{deviceId}/credentials/rotate` with staff bearer token
  permission `devices.credentials.rotate`
- `POST /api/devices/{deviceId}/credentials/{credentialId}/revoke` with staff
  bearer token permission `devices.credentials.revoke`
- SignalR hub at `/hubs/devices`
- SignalR client event `deviceStatusChanged`
- SignalR device command events `deviceCommand` and `deviceCommandResult`

### Operator App

`AFK4.Operator.App` is the native Windows application for operators, cashiers,
managers, technicians, accountants, and owners depending on permissions.

The main working screen is the floor map. The current shell shows static seat
cards, applies SignalR device status updates, stores staff tokens through
Windows-protected storage, and includes a focused technician panel for device
enrollment, command status inspection, and credential lifecycle operations.
Later slices connect session actions, POS, players, shifts, settings, and
role-aware navigation.

### Agent Service

`AFK4.Agent.Service` is the Windows service skeleton for gaming PCs. It creates
heartbeat payloads, sends enrollment-issued device credentials, posts
heartbeats to the backend, and connects to the realtime device hub. Later slices
add installer enrollment bootstrap, local credential lifecycle workflows,
lock/unlock enforcement, process policy, session lease validation, reconnect
reconciliation, watchdog behavior, and updates.

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
- PostgreSQL for runtime device persistence. Set
  `ConnectionStrings__PlatformDatabase` for local API runs that exercise device
  enrollment, credentials, heartbeat state, or command status.

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

For the PostgreSQL-backed device persistence path, first start local
PostgreSQL and apply EF migrations with the
[local PostgreSQL smoke runbook](docs/operations/local-postgres-smoke.md).

Start the backend:

```powershell
dotnet run --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --urls http://localhost:5074
```

Verify health:

```powershell
Invoke-RestMethod http://localhost:5074/api/health
```

Verify the persisted floor map after signing in and seeding branch layout rows:

```powershell
$headers = @{ Authorization = "Bearer <staff-access-token>" }
Invoke-RestMethod http://localhost:5074/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/floor-map -Headers $headers
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

The Agent currently needs enrollment-derived `Agent:DeviceId` and
`Agent:DeviceCredentialSecret` values from configuration or environment
variables before authenticated heartbeats succeed. Real installer bootstrap,
Operator/Agent credential lifecycle workflows, and automated credential
propagation are intentionally deferred to later slices.

## Current Implementation State

The first vertical slice foundation is implemented:

- repository baseline with pinned .NET SDK and shared build settings;
- .NET solution and project structure;
- strong Guid ID primitives;
- shared device and floor map contracts;
- backend health, floor map, heartbeat, and SignalR foundation;
- EF Core/Npgsql device persistence with an initial migration;
- EF Core/Npgsql identity, tenancy, staff access/refresh token, and audit
  baseline;
- staff sign-in and refresh token rotation with opaque hashed tokens;
- predefined MVP role-to-permission mapping;
- branch-scoped authorization for device enrollment-code creation and device
  command dispatch/status endpoints;
- audit records for allowed and denied enrollment-code creation and device
  command dispatch/status attempts;
- branch-scoped authorization and audit for device credential rotation and
  revocation endpoints;
- persisted zones, seats, and explicit device-seat assignments;
- branch-scoped authorization for persisted floor-map reads;
- EF-backed floor-map read model assembled from branch, zone, seat,
  device-seat assignment, and latest device heartbeat state;
- device-authenticated installed apps reporting with EF-backed latest snapshot
  rows;
- staff-protected device detail reads with assigned seat, latest heartbeat,
  active credential count, recent command statuses, and installed app count;
- Operator App Windows-protected token storage abstraction;
- Operator App typed device API client and technician panel for enrollment-code
  creation, command dispatch/status inspection, and credential
  rotation/revocation;
- Operator App technician device detail workflow backed by
  `GET /api/devices/{deviceId}`;
- device enrollment code flow and credential issuance;
- heartbeat and realtime registration credential validation;
- persisted device heartbeat state and command status tracking;
- Agent heartbeat payload factory and worker loop skeleton;
- Operator App floor map shell;
- Player Shell locked-state skeleton.

Not implemented yet:

- staff management workflows, custom roles, and role editing UI;
- authorization coverage for every future operator-facing backend endpoint as
  it is added;
- Operator App sign-in UI and role-aware navigation;
- Operator App layout management UI;
- automatic Agent-side consumption of rotated credentials;
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
