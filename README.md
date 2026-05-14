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
device command status, persisted zones/seats, floor-map reads, shifts, POS
catalog, stock movements, POS sales, manual payments, refunds, voids, and
receipts are backed by EF Core/Npgsql persistence.

The current vertical slice exposes:

- `GET /api/health`
- `POST /api/auth/staff/sign-in`
- `POST /api/auth/staff/refresh`
- `GET /api/branches/{branchId}/floor-map` with staff bearer token permission
  `floor_map.view`
- `POST /api/branches/{branchId}/sessions/start` with staff bearer token
  permission `sessions.start`
- `POST /api/sessions/{sessionId}/extend` with staff bearer token permission
  `sessions.extend`
- `POST /api/sessions/{sessionId}/transfer` with staff bearer token
  permission `sessions.transfer`
- `POST /api/sessions/{sessionId}/end` with staff bearer token permission
  `sessions.end`
- `POST /api/branches/{branchId}/players` with staff bearer token permission
  `players.create`
- `GET /api/players/{playerAccountId}/wallet-summary` with staff bearer token
  permission `billing.view`
- `POST /api/players/{playerAccountId}/wallet/top-ups` with staff bearer
  token permission `billing.wallet.top_up`
- `POST /api/players/{playerAccountId}/ledger/{ledgerEntryId}/refunds` with
  staff bearer token permission `billing.refund`
- `POST /api/players/{playerAccountId}/ledger/manual-corrections` with staff
  bearer token permission `billing.manual_correction`
- `POST /api/players/{playerAccountId}/debts/payments` with staff bearer
  token permission `billing.debt.pay`
- `POST /api/branches/{branchId}/tariffs` with staff bearer token permission
  `tariffs.manage`
- `POST /api/branches/{branchId}/tariffs/{tariffId}/versions` with staff
  bearer token permission `tariffs.manage`
- `POST /api/branches/{branchId}/tariffs/calculate` with staff bearer token
  permission `billing.view`
- `POST /api/branches/{branchId}/packages` with staff bearer token permission
  `packages.manage`
- `POST /api/players/{playerAccountId}/packages/purchases` with staff bearer
  token permission `packages.purchase`
- `GET /api/players/{playerAccountId}/packages` with staff bearer token
  permission `billing.view`
- `POST /api/branches/{branchId}/shifts/open` with staff bearer token
  permission `shifts.open`
- `GET /api/branches/{branchId}/shifts/current` with staff bearer token
  permission `shifts.view`
- `POST /api/shifts/{shiftId}/cash-movements` with staff bearer token
  permission `shifts.cash.manage`
- `POST /api/shifts/{shiftId}/close` with staff bearer token permission
  `shifts.close`
- `POST /api/branches/{branchId}/pos/categories` with staff bearer token
  permission `pos.catalog.manage`
- `POST /api/branches/{branchId}/pos/products` with staff bearer token
  permission `pos.catalog.manage`
- `GET /api/branches/{branchId}/pos/catalog` with staff bearer token
  permission `inventory.view`
- `POST /api/branches/{branchId}/inventory/stock-movements` with staff bearer
  token permission `inventory.stock.manage`
- `POST /api/branches/{branchId}/pos/sales` with staff bearer token
  permission `pos.sales.create`
- `POST /api/pos/sales/{saleId}/payments/manual` with staff bearer token
  permission `pos.sales.pay`
- `POST /api/pos/sales/{saleId}/refunds` with staff bearer token permission
  `pos.sales.refund`
- `POST /api/pos/sales/{saleId}/void` with staff bearer token permission
  `pos.sales.void`
- `GET /api/pos/sales/{saleId}` with staff bearer token permission
  `receipts.view`
- `GET /api/receipts/{receiptId}` with staff bearer token permission
  `receipts.view`
- `POST /api/branches/{branchId}/device-enrollment-codes` with staff bearer
  token permission `devices.enrollment_codes.create`
- `POST /api/devices/enroll`
- `POST /api/devices/{deviceId}/heartbeat`
- `POST /api/devices/{deviceId}/commands/{commandId}/result` with device
  credential authentication
- `POST /api/devices/{deviceId}/session-reconciliation` with device
  credential authentication
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

Wallet/debt summaries and player package remaining time are derived from
immutable `ledger_entries`. The database intentionally does not store mutable
wallet balance, debt balance, or package balance fields.

POS sales are explicit sale records, separate from the billing ledger. Stock on
hand is derived from append-only `stock_movements`. Shift close summary data
reconciles starting cash, cash movements, POS payments/refunds, and
cash-impacting shift-linked ledger entries such as top-ups, debt payments, and
manual corrections. The Phase 6 payment provider is manual/mock only; there are
no fiscal printer, tax authority, card acquirer, or external payment gateway
integrations.

### Operator App

`AFK4.Operator.App` is the native Windows application for operators, cashiers,
managers, technicians, accountants, and owners depending on permissions.

The main working screen is the floor map. The current app includes staff
sign-in, Windows-protected token storage, permission-filtered navigation,
realtime floor-map loading, selected-seat session actions, player search,
wallet/package summaries, POS, shifts, settings, technician device tools, and
production hotkeys.

### Agent Service

`AFK4.Agent.Service` is the Windows service foundation for gaming PCs. It
creates heartbeat payloads, sends enrollment-issued device credentials, posts
heartbeats to the backend, reports Windows installed app inventory snapshots,
connects to the realtime device hub, validates backend-signed session leases,
persists active lease/runtime state for restart recovery, reports reconnect
reconciliation snapshots, drives a testable lock/unlock enforcement
coordinator, supervises the Player Shell process, publishes Shell state over
named pipes, accepts local Shell launcher commands, and applies a local
allow/deny process policy foundation. Later slices add installer enrollment
bootstrap, automatic credential propagation, deeper Windows control, signed
updates, and rollout/rollback behavior.

### Player Shell

`AFK4.Player.Shell` is the player-facing WPF shell shown on gaming PCs. It is a
UI process, not a trusted business authority. The current shell is a fullscreen
MVVM UI for locked, active-session, warning, grace/offline, ending, and launcher
states. It receives Agent-published state through local named pipes and sends
launcher requests back to the Agent for validation.

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
  AFK4.Player.Shell.Tests/

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

Run the Agent Service:

```powershell
dotnet run --project src/AFK4.Agent.Service/AFK4.Agent.Service.csproj
```

The Agent currently needs enrollment-derived `Agent:DeviceId` and
`Agent:DeviceCredentialSecret` values from configuration or environment
variables before authenticated heartbeats succeed. For Phase 8 local
enforcement and Shell IPC, configure `Agent:StateDirectory`,
`Agent:PlayerShellExecutablePath`, optional Shell pipe names, and
`Agent:LauncherApps`. Real installer bootstrap, automatic credential
propagation, signed updates, and rollout/rollback are intentionally deferred to
later slices.

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
- Agent-side Windows installed app inventory collection and authenticated
  reporting to the backend;
- staff-protected device detail reads with assigned seat, latest heartbeat,
  active credential count, recent command statuses, and installed app count;
- Operator App Windows-protected token storage abstraction;
- Operator App typed device API client and technician panel for enrollment-code
  creation, command dispatch/status inspection, and credential
  rotation/revocation;
- Operator App technician device detail workflow backed by
  `GET /api/devices/{deviceId}`;
- shared session lifecycle and signed lease contracts;
- EF-backed sessions, session leases, session events, and session command
  idempotency records with migration `AddSessions`;
- staff-protected guest session start, extend, transfer, and end endpoints;
- session command dispatch through the existing device command path for
  unlock, lock, and lease refresh;
- signed ECDSA session lease issuance by the backend;
- Agent-side signed lease validation and current lease storage;
- device heartbeat/reconnect active lease snapshot fields;
- device-authenticated session reconciliation endpoint with `continue`,
  `unlock`, and `lock` actions;
- floor-map active session projection with remaining seconds;
- immutable billing ledger persistence with player accounts, billing command
  idempotency, tariff versions, package definitions, purchased packages, and
  wallet/debt/package projections derived from `ledger_entries`;
- protected Phase 5 endpoints for player creation, wallet top-ups, refunds,
  manual corrections, debt payments, tariff/version management, tariff
  calculation, package definition/purchase, player package reads, and billing
  session modes;
- session start/extend billing integration for prepaid wallet, postpaid debt,
  and package-backed time consumption;
- shared Phase 6 contracts and permissions for shifts, cash movements, POS
  catalog, stock movements, manual payments, POS sales, refunds, voids, and
  receipts;
- EF-backed shift, cash movement, POS catalog, stock movement, POS sale,
  payment, and receipt persistence with migration
  `AddPosInventoryShiftsReceipts`;
- money-changing ledger entries linked to the currently open shift through
  nullable `ledger_entries.ShiftId`;
- shift service for open/current/cash movement/close flows with idempotency;
- inventory service for branch product catalog and append-only stock
  movements, with stock on hand derived from `stock_movements`;
- POS service for draft sales, manual cash/card-manual payments, stock
  validation, receipts, refunds, voids, and idempotency;
- protected Phase 6 endpoints for shifts, POS catalog, stock movements, POS
  sales, manual payments, refunds, voids, sale reads, and receipt reads;
- authenticated HTTP fallback for device command results returned by heartbeat
  polling;
- device enrollment code flow and credential issuance;
- heartbeat and realtime registration credential validation;
- persisted device heartbeat state and command status tracking;
- Agent heartbeat payload factory and worker loop;
- Agent persistent runtime state and file-backed session lease storage;
- Agent session enforcement coordinator for unlock, lease refresh, lock, and
  lease expiry;
- Agent Player Shell process supervision and local named-pipe state publishing;
- Agent local launcher command handling and allow/deny process policy
  foundation;
- Operator App production floor-map/workflow shell;
- Player Shell fullscreen MVVM session UI with locked, active, warning,
  grace/offline, ending, and launcher states.

Not implemented yet:

- staff management workflows, custom roles, and role editing UI;
- authorization coverage for every future operator-facing backend endpoint as
  it is added;
- Operator App layout management UI;
- automatic Agent-side consumption of rotated credentials;
- audit search and reports;
- deeper Windows lock/unlock enforcement beyond the current MVP-safe adapter
  boundary;
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
