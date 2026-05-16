# AFK4 Platform Architecture Design

## Purpose

AFK4 is a cloud-first SaaS platform for managing computer clubs. The first product release is a serious operator-grade platform, not a web admin panel or a prototype. It targets Windows-based clubs, uses a native Windows Operator App, and controls gaming PCs through installed Windows agents.

The platform is designed to compete architecturally with systems such as Senet, Langame, and SmartShell while keeping the first implementation feasible through a modular monolith backend and strict domain boundaries.

## Core Decisions

- The platform is cloud-first SaaS.
- There is no local club server in the MVP.
- There is no web admin panel in the MVP.
- The operator experience is a native Windows desktop application.
- The first supported gaming PCs are Windows 10/11 only.
- The backend is a .NET ASP.NET Core modular monolith.
- The database is PostgreSQL.
- The Operator App is WPF + MVVM.
- The gaming PC stack is split into Windows Agent Service and Player Shell UI.
- Realtime device communication uses SignalR over WebSockets with fallback behavior.
- Offline behavior is grace mode for already active sessions only.
- Billing supports prepaid, postpaid, packages, bonuses, manual corrections, refunds, and debts through an immutable ledger.
- The platform is multi-tenant from the start.
- POS, inventory, shifts, receipts, and payment provider abstraction are part of the MVP.
- Auto-updates are part of the platform architecture from the start.
- Audit trail is mandatory for critical actions, but full event sourcing is not used in the MVP.

## System Overview

The platform has four main runtime parts.

### Cloud Backend

The Cloud Backend is an ASP.NET Core modular monolith. It is deployed as one backend service at the start, backed by one PostgreSQL database. Internally it is split into strict business modules. Modules do not directly modify each other's tables. Cross-module behavior goes through application services, explicit contracts, and domain events.

The backend owns all business decisions: sessions, billing, tariffs, POS, roles, audit, updates, device commands, and reconciliation. It is the authoritative source of state.

### Operator App

The Operator App is a native Windows application built with WPF + MVVM. It is used by cashiers, operators, administrators, branch managers, technicians, accountants, and owners depending on permissions.

The main screen is the club floor map. Operators can see PC states, start and stop sessions, extend time, transfer players, lock or unlock machines, process payments, sell goods, and close shifts.

The Operator App connects directly to the Cloud Backend through typed APIs and realtime subscriptions. It does not require or depend on a local server.

### Windows Agent Service

The Agent Service is installed on each gaming PC as a Windows Service with elevated privileges. It maintains an outgoing SignalR/WebSocket connection to the cloud, sends heartbeat and device state, receives commands, controls lock and unlock behavior, supervises the Player Shell, applies Windows policies, manages allowed and denied processes, reports installed applications, and participates in updates.

The Agent Service is the local enforcement component, but it is not the business authority. It executes cloud-approved commands and locally enforces signed leases for active sessions during temporary connectivity loss.

### Player Shell

The Player Shell is a separate WPF UI process on the gaming PC. It displays lock state, player login or session code entry, remaining time, warnings, notifications, and a basic launcher for allowed games and applications.

The Shell is not trusted as an authority. It cannot start, extend, or authorize sessions without the Agent Service and Cloud Backend.

## Domain Model

### Tenant And Branch

`Organization` represents a platform tenant: a company, club owner, or network. `Branch` represents one physical club location. Every operational object belongs to an organization and usually to a branch.

The MVP is multi-tenant from the start. Tenant isolation is a core rule, not a later feature.

### Club Layout And Devices

`Zone` groups seats inside a branch, such as main hall, VIP, bootcamp, or console area. `Seat` is the business-level place shown on the floor map. `Device` is the registered Windows PC agent attached to a seat.

The separation between seat and device allows a club to replace a PC, move hardware, or mark a seat as maintenance without corrupting session history.

### Players And Guests

`PlayerAccount` represents a registered player with profile, history, wallet, packages, bonuses, and restrictions. `GuestSession` allows fast operation without registration while still preserving session, payment, and audit records.

The MVP supports both guests and registered players.

### Sessions

`Session` represents usage of a seat/device by a guest or registered player. It stores branch, seat, device, tariff, pricing version, start and end timestamps, current state, lease information, payment mode, and event history.

Core operations are start, extend, transfer, pause where supported, end, force-end, reconcile, and mark failed.

Session state transitions are explicit. The MVP uses states such as requested, active, paused, ending, ended, failed, and reconciled.

### Billing And Ledger

Money is modeled through an immutable ledger, not a mutable balance field. Wallet balance is derived from ledger entries.

Ledger entry types include top-up, gameplay charge, package purchase, package consumption, bonus grant, bonus consumption, refund, manual correction, postpaid debt, debt payment, and reversal.

Errors are corrected by reverse or correction transactions. Historical ledger entries are not edited in place.

### Tariffs And Packages

Tariffs define pricing rules for time usage. Packages define prepaid or promotional bundles, such as hourly bundles, night packages, subscriptions, and bonus time.

Tariff calculation is versioned. A session keeps the tariff rule version used at the time it was started or extended so historical sessions are not recalculated incorrectly after tariff changes.

### POS And Inventory

POS is part of the MVP. The system supports product categories, products, stock, sales, returns, payment methods, receipts, and stock movements.

Gameplay charges, wallet ledger entries, and POS sales are connected but not merged into one model. A drink sale is a POS sale. A gameplay charge is a ledger entry. A session extension may create a ledger entry and a session event.

### Shifts And Cash Register

Operator shifts track who opened the shift, cash operations, sales, refunds, manual corrections, and closing reconciliation.

Shift reports are part of the MVP and must be suitable for day-to-day operator accountability.

### Audit

Audit is a separate immutable trail for critical operations. It records actor, organization, branch, target entity, action, timestamp, source application, device or IP context, and before/after details for configuration, role, tariff, inventory, device, and manual correction changes.

Audit is required for sessions, money, POS, inventory, roles, permissions, tariffs, devices, updates, and manual corrections.

### Updates

Update packages represent signed versions of Operator App, Agent Service, and Player Shell. Rollouts target channels, organizations, branches, device groups, or individual devices.

Rollouts support stable, beta, and internal channels, staged rollout, status tracking, and rollback.

## Backend Modules

### Identity And Access

Owns staff users, authentication, access tokens, device credentials, roles, and permissions. MVP roles are owner, branch manager, shift supervisor, cashier/operator, technician, and accountant/auditor.

The access model is permission-based RBAC with predefined roles in the MVP. Custom roles can be added later without changing the authorization foundation.

### Tenancy

Owns organizations, branches, tenant isolation, tenant-aware middleware, and future tenant subscription state.

Every request must resolve tenant context explicitly. Cross-tenant access is forbidden unless an internal platform operator role is introduced later.

### Club Operations

Owns zones, seats, floor map configuration, seat state projections, branch settings, operator shifts, and operational views.

### Device Management

Owns device enrollment, device identity, heartbeat, state reporting, command dispatch, command status, installed applications, Windows policies, and device update eligibility.

### Sessions

Owns session lifecycle, leases, grace mode, transfers, start and end flows, session event history, and reconciliation after device reconnect.

### Billing And Ledger

Owns wallets, immutable ledger, prepaid and postpaid flows, packages, bonuses, debts, refunds, manual corrections, and idempotency for money-related operations.

### POS And Inventory

Owns product catalog, stock, stock movements, sales, returns, receipt model, payment provider abstraction, and mock/manual payment providers for the MVP.

### Audit

Owns audit event creation and querying. Other modules emit audit records through explicit audit contracts rather than writing audit tables directly.

### Updates

Owns update packages, signing metadata, release channels, rollout rules, rollout status, and rollback state.

### Notifications

Owns system notifications for operators, device alerts, session warnings, and future SMS, Telegram, or email adapters.

## Operator App Design

The Operator App is optimized for dense, repeated operational work rather than marketing-style UI.

Primary screens:

- Floor map as the default working screen.
- Context panel for selected seat, session, player, balance, package, timer, debt, and quick actions.
- POS screen with product selection, cart, payment, returns, and receipt actions.
- Players screen for search, registration, history, balance, packages, and restrictions.
- Shift and reports screen for open shift, close shift, reconciliation, sales, gameplay time, refunds, and operator actions.
- Settings screens for zones, seats, tariffs, roles, products, devices, and updates.

Technical rules:

- Use WPF + MVVM.
- Use typed API clients.
- Use a realtime state store for the floor map.
- Use local read cache only for UI responsiveness, never as financial or session authority.
- Require backend confirmation for critical operations.
- Support hotkeys and fast modal workflows.
- Display pending and failed states explicitly during network issues.

## Agent And Shell Design

The gaming PC runtime is split into an elevated service and a user-facing shell.

Agent Service responsibilities:

- Enroll device using a secure enrollment token or code flow.
- Maintain outgoing SignalR/WebSocket connection.
- Send heartbeat, device state, active session state, and local event backlog.
- Execute cloud commands.
- Enforce lock and unlock.
- Watchdog the Player Shell.
- Apply configured Windows restrictions.
- Manage allow and deny process policies.
- Restore expected state after reboot.
- Report installed applications.
- Install signed updates safely.

Player Shell responsibilities:

- Show locked or unlocked state.
- Show guest or player session entry.
- Show remaining time and warnings.
- Show notifications.
- Provide basic launcher for allowed games and applications.
- Request actions through Agent Service rather than making authoritative decisions.

Security approach:

- Enhanced Windows control is required in the MVP.
- Kernel-level drivers are excluded from the MVP.
- Agent and Shell must be resilient to process restarts.
- Service must be able to restore lock state after reboot.
- Shell must not be trusted for billing, session rights, or policy decisions.

## Realtime Protocol And Reliability

### Channels

REST API is used for commands, settings, POS, reports, reference data, authentication, and authoritative reads.

SignalR over WebSockets is used for realtime floor map state, agent commands, device status, session events, and notifications. The connection is outgoing from the club network, so no inbound ports are required.

Background jobs handle periodic tariff operations, timeout checks, update rollouts, audit processing, report projections, and maintenance work.

### Agent Commands

Every device command includes:

- `commandId`
- command type
- timestamp
- target device
- payload
- expected state or precondition where needed

Agent responses include accepted, completed, failed, or rejected.

Commands must be idempotent. Repeating lock, unlock, end session, or update commands must not corrupt local or cloud state.

The backend keeps command log and execution status.

### Reconnect And Reconciliation

After reconnect, the Agent sends:

- current local device state
- active local session state
- last known lease
- pending local event backlog
- installed component versions

The backend compares local snapshot with cloud state and performs reconciliation. The result may continue, end, relock, unlock, or mark a session as failed/reconciled.

### Operator Reliability

The Operator App does not treat realtime updates as final confirmation for critical operations. Critical operations must be confirmed by authoritative API responses.

Idempotency keys are mandatory for session start, session end, session extension, payments, refunds, POS sales, and manual ledger corrections.

## Grace Mode

Because the platform has no local server, offline mode is intentionally limited.

Allowed during temporary internet loss:

- Continue an already active session within its signed lease.
- Show remaining time based on the lease.
- Keep enforcing lock and unlock based on the last valid session state.
- Record local device/session events for later upload.

Not allowed without cloud connectivity:

- Start a new session.
- Take payment.
- Extend time.
- Apply wallet changes.
- Change tariffs.
- Change roles or permissions.
- Perform POS sales.

The lease is signed by the backend and verified by the Agent. The Agent cannot create or extend leases by itself.

## Data And Transactions

The MVP uses PostgreSQL as the source of truth. EF Core is used for migrations.

Data is tenant-aware through organization and branch identifiers plus strict access filters. Modules may use separate database schemas or explicit table prefixes to maintain ownership boundaries.

Read models and projections are allowed for floor map, reports, and dashboards. They are not the write authority.

Critical data rules:

- Ledger entries are immutable.
- Corrections are represented by reversal or correction entries.
- POS sale states include draft, pending payment, paid, refunded, and voided.
- Session state transitions are explicit.
- Tariff and package rules are versioned.
- Audit is written for critical changes.
- Idempotency keys are mandatory for critical operations.
- Reports should be served from read models or aggregations where live joins would be fragile or slow.

Redis can be added later for caching and distributed coordination. It is not a source of truth.

## Deployment And Updates

### Environments

Development uses local services, Docker PostgreSQL, and mock/manual payment and receipt providers.

Staging mirrors production closely enough to test migrations, rollout behavior, Agent updates, and reconciliation.

Production runs the multi-tenant SaaS platform.

### Backend

The backend starts as one ASP.NET Core deployment. Background workers can run in the same process or a separate worker process depending on operational needs.

The backend must include structured logging, health checks, metrics-ready design, and externalized secrets.

### Client Updates

Operator App, Agent Service, and Player Shell updates are centrally managed.

Update requirements:

- signed packages
- stable, beta, and internal channels
- staged rollout
- targeting by tenant, branch, device group, or device
- rollout status tracking
- rollback
- safe Agent update behavior that does not leave a PC accidentally unlocked or unmanaged

### Installers

The MVP client packaging baseline is WiX-authored MSI packages.

Operator App has its own WiX/MSI installer. Agent Service and Player Shell
share one coordinated gaming-PC WiX/MSI installer because the gaming-PC surface
needs per-machine installation, Windows Service registration, controlled Shell
deployment, recovery metadata, and silent install support. MSIX is deferred as
a future optional Operator App distribution channel and is not used for Agent
Service or Player Shell in the MVP.

Update artifacts continue to use the central update package model: externally
hosted binary artifact, SHA-256 hash, signed package metadata, staged rollout,
status reporting, and rollback. Agents invoke MSI install or rollback through
the existing external install, rollback, and restart adapter boundary after
package hash and signature verification.

Device enrollment uses a secure branch/device enrollment token, QR code, or
short code flow. Installers must not ship durable device credentials or signing
keys.

## Security Baseline

Security is part of the MVP architecture, not a later hardening pass.

Authentication and authorization:

- staff users authenticate against the Cloud Backend;
- predefined roles map to explicit permissions;
- backend endpoints enforce tenant, branch, role, and permission checks;
- Operator App stores tokens using Windows-protected storage;
- refresh token rotation is required for long-lived operator sessions;
- device credentials are separate from staff credentials.

Device enrollment:

- Agent enrollment uses a short-lived enrollment token or code created by an authorized operator or technician;
- the backend issues a durable device credential after enrollment;
- device credentials can be revoked from the Operator App;
- every device request includes device identity and tenant/branch context;
- backend rejects device requests where route identity, credential identity, and payload identity do not match.

Operational security:

- secrets are never stored in repository config files;
- password hashing uses a modern .NET-supported password hasher or dedicated identity provider;
- audit records are written for failed privileged operations as well as successful ones;
- critical money, session, and device commands require idempotency keys;
- update packages must be signed before production rollout.

## API And Contract Strategy

The platform uses explicit contracts between backend, Operator App, Agent Service, and Player Shell.

Rules:

- shared DTOs live in a dedicated contracts project;
- API contracts are versioned before production external integrations are introduced;
- internal module models are not exposed directly as API responses;
- device commands use stable command names and explicit payload schemas;
- breaking contract changes require a compatibility plan for older Operator App and Agent versions;
- public integration APIs are deferred until the core product stabilizes.

The first implementation can use simple REST endpoints and SignalR messages, but the structure must keep a clean separation between domain models, application commands, and transport DTOs.

## Developer Experience And Quality Gates

The repository must be comfortable for long-term product development.

Baseline requirements:

- one solution file for the first platform slice;
- separate projects for building blocks, contracts, backend, Operator App, Agent Service, and Player Shell;
- test projects aligned with production projects;
- pinned .NET SDK through `global.json`;
- shared build settings through `Directory.Build.props`;
- nullable reference types enabled;
- warnings treated as errors;
- short, focused modules instead of large cross-cutting files;
- clear README with local setup and first-run commands.

Quality gates:

- every domain rule starts with a focused test;
- backend endpoints get integration tests;
- contract serialization gets tests before clients depend on DTOs;
- Agent behavior gets unit tests around payloads, command handling, leases, and reconciliation;
- Operator App view models get unit tests independent of WPF rendering;
- full solution build and test suite are required before claiming a slice is complete.

## Observability And Operations

The backend and installed clients must be diagnosable from the start.

Backend observability:

- structured logs with tenant, branch, actor, device, session, and correlation context where applicable;
- health endpoint;
- readiness checks once PostgreSQL and external providers are introduced;
- metrics-ready design for sessions, device connectivity, command latency, payment failures, and update rollout status;
- correlation IDs across Operator App requests, backend logs, and device commands.

Agent observability:

- local service logs;
- heartbeat status;
- last command status;
- update status;
- last successful cloud connection timestamp;
- local event backlog size.

Operator observability:

- visible pending and failed operation states;
- actionable errors for network, permission, payment, and device command failures;
- support diagnostics screen in a later UI slice.

## Data Protection And Recovery

The MVP must be built assuming real clubs will depend on the data.

Requirements:

- PostgreSQL backups are mandatory before production;
- migrations must be repeatable in staging before production rollout;
- audit and ledger records are append-only;
- tenant data export is a future administrative requirement;
- accidental destructive operations should be represented as reversals, voids, or corrections rather than deletes;
- production secrets and signing keys must be stored outside source control;
- update rollback must be tested before production Agent rollout.

## MVP Scope

The first full MVP includes:

- multi-tenant organizations and branches
- staff users, predefined roles, and permission-based access
- WPF Operator App with floor map
- zones, seats, and Windows devices
- Agent Service and Player Shell
- SignalR realtime status and commands
- session start, extend, transfer, and end
- guest sessions and registered players
- mixed billing foundation
- immutable ledger
- tariffs and packages
- basic POS with catalog, stock, sales, returns, payment methods, and receipts
- shifts and cash register flows
- receipt and payment provider abstraction with mock/manual providers
- basic launcher
- enhanced Windows control without kernel driver
- grace mode for active sessions
- audit trail
- centralized updates with channels, rollout, and rollback
- basic reports for shifts, sales, gameplay time, cash operations, and operator actions

The MVP does not include:

- web admin panel
- local club server
- Linux or macOS agents
- kernel-level anti-bypass driver
- full game library management with Steam, Epic, or Battle.net auto-updates
- country-specific fiscal integrations
- SMS, Telegram, or email integrations
- mobile player app
- microservices
- full-domain event sourcing
- advanced CRM or loyalty beyond basic bonuses and packages

## Delivery Plan Set

The architecture spec intentionally covers more than the first implementation plan. The MVP should be delivered through a sequence of focused plans, each producing working, testable software.

Recommended plan sequence:

1. **Vertical Slice Foundation**
   Repository, solution structure, shared contracts, backend health/floor-map endpoints, SignalR heartbeat, Agent skeleton, Operator App shell, Player Shell skeleton.

2. **Identity, Tenancy, And RBAC**
   Staff users, organizations, branches, predefined roles, permission checks, token storage, tenant-aware API pipeline, audit for privileged actions.

3. **Club Layout And Device Management**
   zones, seats, device enrollment, device credentials, device state, command log, command status, installed apps, device detail screens.

4. **Session Lifecycle And Grace Mode**
   start, extend, transfer, end, session state machine, signed leases, Agent lease validation, reconnect reconciliation, session audit.

5. **Billing, Ledger, Tariffs, And Packages**
   immutable ledger, prepaid/postpaid flows, packages, bonuses, debts, refunds, manual corrections, tariff versioning, idempotency.

6. **POS, Inventory, Shifts, And Receipts**
   products, categories, stock, sales, returns, shift open/close, cash reconciliation, receipt/payment provider abstraction with mock/manual providers.

7. **Operator App Production UX**
   real floor map state, context panel actions, POS workflows, player search, shift workflows, settings, role-aware navigation, hotkeys.

8. **Agent Enforcement And Player Shell**
   lock/unlock enforcement, watchdog, Windows policies, process allow/deny lists, reboot recovery, Shell session screen, basic launcher.

9. **Updates And Installers**
   signed packages, channels, rollout targeting, rollback, Operator installer, Agent/Shell installer, enrollment flow.

10. **Reports, Audit Review, And Operations**
    shift reports, sales reports, gameplay reports, operator action reports, audit search, diagnostics, backup/restore runbooks.

This split is deliberate. A single implementation plan for the whole MVP would be too large to execute safely and would mix unrelated risk areas.

## First Implementation Direction

Implementation should start with repository and solution scaffolding, not UI polish.

The first implementation slice is the **Vertical Slice Foundation** from the delivery plan set. It is not expected to complete billing, POS, identity, updates, or full device enforcement.

Recommended first slice:

1. Create the .NET solution and project structure.
2. Add backend modular monolith foundation.
3. Add shared contracts and domain primitives.
4. Add identity, tenancy, branch, zone, seat, and device foundations.
5. Add basic Operator App shell with an initial floor map view showing static seat cards.
6. Add Agent Service enrollment and heartbeat skeleton.
7. Add SignalR device status flow.
8. Add session lifecycle foundation.
9. Add ledger foundation.
10. Add POS foundation.

This order creates a working vertical path from cloud to Operator App to Agent before expanding billing, POS, updates, and reports.

## Open Decisions For Future Specs

The following decisions are intentionally deferred to focused implementation specs:

- WPF UI component library and visual design system.
- Exact PostgreSQL schema layout by module.
- Exact API route and contract naming.
- Agent Windows policy implementation details.
- Payment and receipt provider plugin contract.
- First production hosting provider and deployment topology.
- Optional future MSIX/App Installer distribution channel for Operator App.
