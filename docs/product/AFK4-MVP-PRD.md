# AFK4 MVP Product Requirements Document

Status: Draft for product review  
Last updated: 2026-05-20

## 1. Product Summary

AFK4 is a cloud-first SaaS platform for managing Windows-based computer clubs.
It is designed for real club operations: session control, PC state visibility,
operator workflows, payments, POS, shifts, audit, updates, and Windows endpoint
control.

AFK4 is not a lightweight admin panel and not a local-only club server. The MVP
uses a native Windows Operator App, an ASP.NET Core cloud backend, a Windows
Agent Service on gaming PCs, and a Player Shell UI. The product should feel like
serious platform software in the same category as Senet, Langame, and
SmartShell, while keeping the first release focused enough to ship safely.

The first MVP must prove that a club can run day-to-day operations through AFK4:
open a shift, see the floor map, start and end sessions, accept payments, sell
POS items, control Windows PCs, handle temporary connectivity loss, close a
shift, audit critical actions, and centrally update installed client software.

## 2. Problem Statement

Computer clubs need one operational system that keeps money, sessions, devices,
and operator accountability consistent. Fragmented tooling creates operational
risk:

- Operators can start, extend, or end sessions incorrectly.
- PC lock state can drift from paid session state.
- Cash, POS sales, gameplay charges, and refunds are hard to reconcile.
- Device status is not visible enough for fast troubleshooting.
- Manual corrections are difficult to audit.
- Tariff and package changes can corrupt historical interpretation if not
  versioned.
- Local-only systems complicate multi-branch visibility and centralized
  updates.
- Web-only tools are not ergonomic enough for fast cashier/operator workflows.

AFK4 should reduce these risks by making the cloud backend the business
authority, keeping the Operator App fast and native, and making every critical
money, session, POS, device, and configuration action explicit and auditable.

## 3. Target Users And Roles

### Owner

Owns one or more organizations or branches. Needs high-level visibility into
revenue, utilization, shifts, inventory, staff activity, device health, and
rollouts.

Primary needs:

- multi-branch visibility;
- reliable financial and operational reports;
- role and permission control;
- audit history for sensitive actions;
- confidence that devices and updates are centrally managed.

### Branch Manager

Runs day-to-day branch operations. Needs control over floor layout, tariffs,
staff, devices, inventory, shifts, and issue handling.

Primary needs:

- configure zones, seats, and devices;
- review shift performance;
- manage staff behavior and corrections;
- monitor device and session problems;
- maintain stock and POS catalog.

### Shift Supervisor

Oversees a working shift and handles escalations. Needs fast visibility into
active sessions, operator actions, cash state, refunds, corrections, and device
issues.

Primary needs:

- approve or perform sensitive corrections;
- handle failed payments, session disputes, and device errors;
- close shifts with accurate reconciliation.

### Cashier / Operator

Works the front desk. Needs the fastest possible path for common workflows:
start sessions, extend time, accept payments, sell goods, move players, end
sessions, and answer basic player questions.

Primary needs:

- floor map as the default screen;
- fast guest and registered player flows;
- clear payment and POS actions;
- visible pending and failed states;
- low-friction shift workflows.

### Technician

Maintains PCs and installed software. Needs device state, enrollment, lock
state, installed component versions, and update status.

Primary needs:

- enroll and replace devices;
- troubleshoot offline or failed devices;
- see Agent and Shell versions;
- apply or monitor update rollout;
- inspect device command status.

### Accountant / Auditor

Reviews financial and operational history. Needs immutable financial records,
audit history, shift reports, POS reports, and correction history.

Primary needs:

- ledger-backed financial data;
- shift close reports;
- POS sales and returns;
- gameplay charge history;
- manual correction and refund audit.

### Player / Guest

Uses a gaming PC through a controlled Player Shell. May be anonymous for guest
sessions or registered with account balance, packages, bonuses, and history.

Primary needs:

- clear locked and active session state;
- visible remaining time and warnings;
- basic launcher for allowed apps;
- simple session start/login flow in later releases.

## 4. MVP Goals And Non-Goals

### MVP Goals

The first full MVP must allow a Windows-based computer club to run core
operations through AFK4:

- manage organizations, branches, staff users, predefined roles, and
  permissions;
- manage zones, seats, and Windows devices;
- show a usable native Windows Operator App floor map implemented through a
  WebView2 desktop shell and React/TypeScript UI;
- run Agent Service and Player Shell on gaming PCs;
- report device status and receive cloud-approved commands through realtime
  infrastructure;
- start, extend, transfer, and end sessions;
- support guest sessions and registered player accounts;
- support prepaid, postpaid, packages, bonuses, debts, refunds, and manual
  corrections through an immutable ledger;
- manage tariffs and packages with versioned calculation;
- provide basic POS with products, categories, stock, sales, returns, payment
  methods, and receipts;
- support operator shifts and cash reconciliation;
- provide receipt and payment provider abstraction with mock/manual providers;
- provide basic launcher and enhanced Windows control without a kernel driver;
- support grace mode for already active sessions during temporary connectivity
  loss;
- write audit records for critical actions;
- centrally manage signed updates with channels, staged rollout, status, and
  rollback;
- provide basic reports for shifts, sales, gameplay time, cash operations, and
  operator actions.

### Non-Goals For The First MVP

The first MVP intentionally excludes:

- web admin panel;
- local club server;
- Linux or macOS agents;
- kernel-level anti-bypass driver;
- full Steam, Epic, or Battle.net game library management and auto-updates;
- country-specific fiscal integrations;
- SMS, Telegram, or email integrations;
- mobile player app;
- microservices;
- full-domain event sourcing;
- advanced CRM and loyalty beyond basic bonuses and packages.

## 5. Core User Journeys

### Journey 1: Open A Shift

1. Operator signs in to the native Operator App.
2. Backend resolves organization, branch, role, and permissions.
3. Operator opens a shift with starting cash amount.
4. System records shift open event and audit trail.
5. Operator lands on the floor map and can begin normal work.

Success criteria:

- shift state is visible in the Operator App;
- all money and POS operations are linked to the open shift;
- opening a shift is auditable.

### Journey 2: Start A Guest Session

1. Operator selects a free seat from the floor map.
2. Operator chooses guest session and tariff/package/payment mode.
3. Backend validates branch, seat, device, tariff, shift, and payment state.
4. Backend creates the session and any required ledger entries.
5. Backend sends the device unlock command.
6. Agent unlocks the PC and reports command status.
7. Floor map updates session and device state.

Success criteria:

- no session starts without backend approval;
- money and session state are consistent;
- device state becomes visible to the operator;
- duplicate requests do not create duplicate sessions or charges.

### Journey 3: Start A Registered Player Session

1. Operator searches for or creates a player account.
2. Operator selects player wallet, package, bonus, or postpaid mode where
   allowed.
3. Backend validates player restrictions, balance/package state, tariff, and
   device availability.
4. Backend starts the session and creates required ledger/session events.
5. Agent unlocks the PC and Player Shell displays session state.

Success criteria:

- wallet/package usage is ledger-backed;
- player history remains traceable;
- tariff version is preserved for the session.

### Journey 4: Extend, Transfer, Or End Session

1. Operator selects an active session.
2. Operator chooses extend, transfer, pause where supported, end, or force-end.
3. Backend validates state transition and permissions.
4. Backend applies ledger/session changes and sends device command if needed.
5. Operator sees pending, success, or failed status.

Success criteria:

- invalid state transitions are rejected;
- money adjustments are immutable ledger entries;
- device commands are idempotent;
- session history remains complete.

### Journey 5: Sell POS Items

1. Operator opens POS workflow during an active shift.
2. Operator adds products to cart.
3. Backend validates stock and prices.
4. Operator takes payment through configured mock/manual provider in MVP.
5. Backend records sale, payment, stock movement, receipt, and shift linkage.

Success criteria:

- stock changes are auditable;
- sale states are explicit;
- returns and voids do not delete history.

### Journey 6: Refund Or Manual Correction

1. Authorized staff member opens a session, ledger entry, POS sale, or shift
   item.
2. Staff member chooses refund, reversal, void, or manual correction.
3. Backend validates permission and requires reason.
4. Backend creates reversal/correction records rather than editing history.
5. Audit trail captures actor, target, before/after context, and reason.

Success criteria:

- financial history is immutable;
- correction is traceable and reportable;
- unauthorized staff cannot perform sensitive changes.

### Journey 7: Device Goes Offline During Active Session

1. Agent loses cloud connectivity.
2. Existing active session continues only within last valid signed lease.
3. Player Shell shows state based on local lease.
4. Agent records local event backlog.
5. After reconnect, Agent sends local state and backlog.
6. Backend reconciles cloud and local state.

Success criteria:

- no new sessions, payments, POS sales, or time extensions happen offline;
- existing active session does not immediately fail due to short outage;
- backend remains the authority after reconnect.

### Journey 8: Close A Shift

1. Operator or supervisor initiates shift close.
2. System summarizes gameplay charges, POS sales, refunds, cash movements,
   manual corrections, and expected cash.
3. Staff enters counted cash and reconciliation notes.
4. Backend closes shift and writes audit records.
5. Reports become available for manager/accountant review.

Success criteria:

- shift cannot close with unresolved blocking inconsistencies unless an
  authorized override is recorded;
- report data is stable after close;
- corrections after close remain explicit.

### Journey 9: Roll Out Client Update

1. Authorized user uploads or selects a signed package.
2. User chooses channel and rollout target.
3. Backend starts staged rollout.
4. Agent reports download, install, success, failure, or rollback state.
5. Operator/technician can see rollout status.

Success criteria:

- unsigned packages cannot be deployed to production;
- rollout can target tenant, branch, group, or device;
- rollback is supported;
- update behavior must not leave a PC unmanaged or accidentally unlocked.

## 6. Functional Requirements

### Identity, Tenancy, And RBAC

- The system must support organizations and branches.
- Staff users must authenticate against the backend.
- Predefined MVP roles must include owner, branch manager, shift supervisor,
  cashier/operator, technician, and accountant/auditor.
- Authorization must be permission-based, not hardcoded only by role name.
- Every business request must resolve tenant and branch context.
- Cross-tenant access must be rejected by default.
- Critical privileged actions must write audit records.

### Club Layout And Device Management

- Branches must contain zones and seats.
- Seats must be separate from physical devices.
- A Windows device must be enrollable and attachable to a seat.
- Device state must include online/offline, lock state, Agent version, Shell
  version, and last heartbeat.
- Backend must store command status for device actions.
- Device identity must be separate from staff identity.

### Session Lifecycle

- The MVP must support session start, extend, transfer, pause where supported,
  end, force-end, and reconciliation.
- Session states must be explicit: requested, active, paused, ending, ended,
  failed, and reconciled.
- Session commands must be idempotent.
- Session must preserve tariff rule version used at start or extension time.
- Critical session actions must require backend confirmation.

### Billing, Ledger, Tariffs, And Packages

- Ledger entries must be immutable.
- Wallet balance must be derived from ledger entries.
- Supported ledger entry types must include top-up, gameplay charge, package
  purchase, package consumption, bonus grant, bonus consumption, refund, manual
  correction, postpaid debt, debt payment, and reversal.
- Errors must be corrected through reversal or correction entries.
- Tariff calculation must be versioned.
- Packages and bonuses must be represented explicitly and auditable.
- Payments and money-related operations must use idempotency keys.

### POS, Inventory, Shifts, And Receipts

- Product catalog must support categories and products.
- Inventory must support stock movements.
- POS sale states must include draft, pending payment, paid, refunded, and
  voided.
- Returns and voids must not delete original sale records.
- Sales must link to shift, operator, payment method, and receipt record.
- Receipt and payment providers must be abstracted.
- MVP must include mock/manual providers before country-specific integrations.

### Audit

- Audit trail must be immutable for critical operations.
- Audit records must include actor, organization, branch, action, target,
  timestamp, source app, device/IP context where available, and before/after
  details where relevant.
- Audit must cover sessions, money, POS, inventory, roles, permissions,
  tariffs, devices, updates, and manual corrections.

### Updates

- Operator App, Agent Service, and Player Shell must support centralized
  updates.
- Packages must be signed before production rollout.
- Channels must include stable, beta, and internal.
- Rollout must support targeting by tenant, branch, device group, and device.
- Rollout status and component versions must be visible.
- Rollback must be supported.

### Reports

- MVP reports must include shift report, sales report, gameplay time report,
  cash operations report, and operator actions report.
- Critical reports should use read models or aggregations where live joins would
  be fragile or expensive.
- Reports must preserve historical interpretation of tariffs, packages,
  payments, and corrections.

### Operator App

- Operator App must remain a native Windows desktop application, not a browser
  web admin panel.
- Operator App must use a .NET desktop shell with WebView2 hosting a
  React/TypeScript UI for the operator experience.
- The WebView2 shell owns Windows integration such as process lifetime,
  protected token storage, environment configuration, native packaging, and
  safe host-to-web bridges. Business state remains backend-authoritative.
- Floor map must be the default working screen.
- Operator App must show pending and failed states explicitly.
- Critical actions must wait for backend confirmation.
- Operator App must support fast workflows for session actions, POS, players,
  shifts, and settings.
- Local cache may improve UI responsiveness but must not be financial or
  session authority.

### Agent Service And Player Shell

- Agent Service must run on Windows 10/11 gaming PCs.
- Agent Service must maintain outgoing cloud communication.
- Agent Service must send heartbeat, device state, active session state, and
  local event backlog.
- Agent Service must execute backend-approved commands.
- Agent Service must enforce lock/unlock state and restore expected state after
  reboot.
- Agent Service must supervise Player Shell.
- Player Shell must show locked/session state, remaining time, warnings, and
  launcher UI.
- Player Shell must not be trusted for billing, authorization, or session
  rights.

## 7. Non-Functional Requirements

### Security

- Staff authentication and device authentication must be separate.
- Operator tokens must be stored using Windows-protected storage.
- Refresh token rotation is required for long-lived operator sessions.
- Device enrollment must use short-lived enrollment token, QR, or code flow.
- Device credentials must be revocable.
- Backend must reject requests where route identity, credential identity, and
  payload identity conflict.
- Secrets must not be stored in repository config files.

### Reliability

- Backend is the source of truth for sessions, money, POS, and device commands.
- Realtime updates are not final confirmation for critical actions.
- Idempotency is required for critical money, POS, session, and device commands.
- Agent reconnect must support reconciliation.
- Grace mode must only continue already active sessions within signed lease
  limits.

### Performance

- Floor map should update quickly enough for front-desk operation.
- Common operator actions should avoid unnecessary multi-screen navigation.
- Reports should not depend on fragile live joins in critical workflows.
- Backend should be structured to add read models and caching without changing
  business authority.

### Observability

- Backend logs must be structured and include tenant, branch, actor, device,
  session, and correlation context where applicable.
- Backend must expose health checks.
- Agent must expose or log heartbeat state, last successful cloud connection,
  command status, update status, and local event backlog size.
- Operator App must display actionable errors for network, permission, payment,
  and device command failures.

### Data Protection

- PostgreSQL backups are mandatory before production.
- Migrations must be tested in staging before production rollout.
- Audit and ledger records must be append-only.
- Destructive operations should be represented as reversals, voids, or
  corrections rather than deletes.
- Signing keys and production secrets must be stored outside source control.

## 8. MVP Release Phases

### Phase 1: Vertical Slice Foundation

Repository, solution, shared contracts, health endpoint, floor map endpoint,
heartbeat endpoint, SignalR foundation, Agent skeleton, Operator shell, Player
Shell skeleton.

### Phase 2: Identity, Tenancy, And RBAC

Organizations, branches, staff authentication, predefined roles, permissions,
tenant-aware pipeline, token storage, and audit for privileged actions.

### Phase 3: Club Layout And Device Management

Zones, seats, device enrollment, device credentials, device state, command log,
command status, installed apps, and device detail workflows.

### Phase 4: Session Lifecycle And Grace Mode

Start, extend, transfer, end, session state machine, signed leases, Agent lease
validation, reconnect reconciliation, session audit.

### Phase 5: Billing, Ledger, Tariffs, And Packages

Immutable ledger, prepaid and postpaid flows, wallet, packages, bonuses, debts,
refunds, manual corrections, tariff versioning, and idempotency.

### Phase 6: POS, Inventory, Shifts, And Receipts

Product catalog, stock, sales, returns, shift open/close, cash reconciliation,
receipt/payment provider abstraction, and mock/manual providers.

### Phase 7: Operator App Production UX

Realtime floor map state, context panel actions, POS workflow, player search,
shift workflow, settings, role-aware navigation, and hotkeys.

### Phase 8: Agent Enforcement And Player Shell

Lock/unlock enforcement, watchdog, Windows policies, process allow/deny lists,
reboot recovery, Player Shell session screen, and basic launcher.

### Phase 9: Updates, Reports, And Operations

Signed packages, channels, rollout targeting, rollback, installers, audit
search, reports, diagnostics, backup/restore runbooks.

## 9. Success Metrics

The MVP should be evaluated by operational correctness and speed, not only by
feature count.

Product success indicators:

- operator can start a normal session from floor map with minimal steps;
- operator can extend and end sessions without state ambiguity;
- device lock state is visible and reconciles with backend session state;
- shift close report matches gameplay, POS, refunds, and cash movements;
- ledger-derived balances remain consistent after refunds and corrections;
- device heartbeat and command state are visible to operator/technician;
- temporary connectivity loss does not break already active sessions inside
  lease limits;
- update rollout status is visible and rollback path exists;
- audit trail can explain who changed money, sessions, devices, tariffs, roles,
  and inventory.

Candidate measurable targets for later validation:

- start guest session in under 15 seconds for a trained operator;
- floor map state update visible within 2 seconds under normal connectivity;
- 100 percent of money-changing operations represented by ledger entries;
- 100 percent of critical admin actions represented by audit records;
- shift close report generated in under 5 seconds for a normal branch day;
- successful staged update status reported for every targeted device.

## 10. Risks And Open Decisions

### Windows Enforcement Complexity

Enhanced Windows control without a kernel driver is required for the MVP, but
bypass resistance and reboot recovery need focused design and testing.

### Fiscal And Payment Providers

The MVP starts with mock/manual providers. Country-specific fiscal integrations
are intentionally deferred, but the provider abstraction must not block them.

### Offline Expectations

Because there is no local server, offline behavior is intentionally limited.
This must be communicated clearly to operators and owners to avoid mismatched
expectations.

### Tenant Isolation

Tenant isolation is required from the start. Mistakes here are high impact and
must be covered by architecture, tests, and review.

### Update Safety

Agent updates must not leave a PC unmanaged, unlocked, or half-updated. Rollback
and status reporting need dedicated implementation plans.

### UI Workflow Complexity

The Operator App must stay fast and dense without becoming confusing. Complex
flows such as refunds, transfer, postpaid debt, package consumption, and shift
close need focused UX design.

### Operator App UI Runtime Migration

The MVP Operator App runtime is changing from WPF-rendered screens to a .NET
desktop WebView2 shell with React/TypeScript UI. The migration must preserve the
native Windows app boundary, protected token storage, packaging/update model,
backend-authoritative critical actions, and staging smoke capability while
removing WPF as the primary operator UI technology.

### Realtime Agent Protocol

The vertical slice includes HTTP heartbeat plus backend SignalR broadcast. The
full MVP requires an outgoing Agent SignalR/WebSocket connection for command and
state flow. This needs a dedicated follow-up plan.

## 11. References

- [Project README](../../README.md)
- [Architecture spec](../superpowers/specs/2026-05-12-afk4-platform-architecture-design.md)
- [Operator App WebView2 React migration plan](../superpowers/plans/2026-05-20-operator-app-webview2-react-migration.md)
- [Vertical slice implementation plan](../superpowers/plans/2026-05-12-afk4-platform-vertical-slice.md)
- [Agent instructions](../../AGENTS.md)
