# Operator App WebView2 React Migration Plan

> **For agentic workers:** implement this plan task-by-task. Keep the native
> Windows desktop app boundary. Do not introduce a browser-delivered web admin
> panel or local club server.

**Goal:** Replace the WPF-rendered Operator App UI with a .NET Windows desktop
host that embeds WebView2 and serves a built React/TypeScript operator UI from
local application assets.

**Architecture:** Keep `src/AFK4.Operator.App` as the native Windows desktop
application and MSI/update component. The .NET host owns window lifecycle,
environment configuration, protected token storage, WebView2 initialization,
and narrow host bridges. React/TypeScript owns operator screens, layout,
frontend state, API calls, SignalR client state, and visual implementation of
`docs/product/operator-app-ui-target.md`.

**Tech Stack:** .NET 10 Windows desktop host, WebView2, React, TypeScript,
Vite, typed API client layer, SignalR JavaScript client, xUnit for host tests,
Vitest/React Testing Library for frontend unit tests, Playwright for local UI
smoke where practical, WiX/MSI packaging.

## Continuation Kickoff

Continue from branch `codex/operator-app-redesign`.

Current state:

- `src/AFK4.Operator.App` starts the new WebView2 shell instead of the legacy
  WPF `MainWindow`.
- `src/AFK4.Operator.App.Web` contains the React/Vite frontend and local
  SmartShell-inspired fixture screens for Map, Dashboard, Booking, POS/Shop,
  Clients, Payments, Logs, and Settings.
- As of 2026-05-21, Dashboard, Map, Booking, POS, Clients, Payments, Logs, and
  Settings have had fixture-level design passes. Settings is intentionally
  simpler and owner/admin-facing rather than a dense operations grid, and should
  not inherit shared top status strips or period controls unless a concrete
  settings subsection needs them. Header controls should only be used when they
  change screen data or trigger a clear action; unwired top pseudo-tabs were
  removed from the reviewed fixtures, while operational summary strips remain
  where they provide quick context. A same-day motion/interaction pass added
  fixture-local selections, filters, carts, payment/source/section choices,
  action feedback notices, animated counters/donut values, restrained
  transitions, and reduced-motion handling. These passes are visual/interaction
  direction only; backend-backed parity is still pending.
- The fixture design phase is closed for this branch as of 2026-05-21. Treat
  the current Map, Dashboard, Booking, POS, Clients, Payments, Logs, and
  Settings screens as the accepted design baseline while replacing fixture
  state with real backend state. Reopen design only for concrete defects found
  during real-data or staging smoke.
- The current UI is no longer purely fixture-backed for the primary map:
  native protected-token bridge, staff sign-in, common API/error projection,
  typed frontend API client boundaries, SignalR device-status state, and
  backend-backed floor-map loading now exist. Backend-confirmed selected-seat
  fast guest start, extend, transfer, and end actions now exist for backend
  floor-map seats. POS, Clients, Payments, Logs, and Settings now have first
  backend-backed React parity wiring against existing endpoints. Booking now
  uses real reservation contracts for search/create/update/confirm/seat/cancel.
  The React shell also has first-pass permission-aware workspace navigation and
  selected-seat session action state. The map panel now supports guest,
  prepaid-wallet, package, and postpaid billing selection for start/extend, and
  reads selected-seat device command result status after session commands. The
  primary map now also has real filters and table view parity over the same
  backend-loaded seat state. Booking now has first-pass permission/state guards
  for mutation controls. Settings now has a backend-backed staff creation form
  in the Personnel section, branch profile save backed by a new Platform API
  profile endpoint, and POS category/product creation backed by the existing
  POS catalog endpoints. Payments can now close the current shift through the
  existing close-shift endpoint with counted cash and a closing note, and can
  record cash movements through the existing shift cash movement endpoint.
  Staging smoke across the newly wired screens is the next real implementation
  work.
  Fixture-only or missing-contract commands must not display backend success
  while that wiring is missing.
- Legacy WPF screens/ViewModels remain in the repository as parity reference
  until the WebView2/React day flow covers pilot operations.

Next-session kickoff:

The design phase is done. Do not reopen broad fixture polish. The accepted
baseline is the current WebView2/React Operator UI: Map as the primary
workspace, plus Dashboard, Booking, POS, Clients, Payments, Logs, and Settings
with the reviewed dense operator layout, Russian-first copy, local
interactions, action feedback, animated values, and reduced-motion support.

Continue turning this fixture UI into a real operator app without changing the
approved visual direction. The native protected-token bridge, staff sign-in,
auth client, common error projection, authenticated HTTP helper, typed API
client boundaries, SignalR realtime state, and backend-backed floor-map data
loading are now in place. Backend-confirmed floor-map start, extend, transfer,
and stop actions are now wired for backend-loaded seats. POS checkout,
clients/wallet, payments/reports, logs/audit/diagnostics, and settings read
surfaces now call existing backend endpoints. Dashboard now has a backend
summary endpoint and React wiring for active shift, revenue/utilization, alert
pressure, focus queue, recent payments, and export fetches. Booking has real
reservation API wiring, restored staff permissions now gate workspace
navigation plus selected-seat session actions, the map panel now sends billed
session metadata plus displays device command status feedback, and the primary
map has real filter/table parity. Booking mutation controls now also respect
`reservations.manage` and selected reservation state. Settings Personnel can
create branch staff through the existing backend staff API, and profile save
now updates branch name/city through the backend. Settings `POS и склад` can
also create a backend POS category and product. Payments close-shift now calls
the backend close-shift endpoint with counted cash and note fields, and
Payments cash movement creation calls the backend shift cash endpoint. Continue
with staging smoke of the backend-backed workspaces and close gaps found with
real data.

The backend remains authoritative for sessions, money, POS, shifts, devices,
and critical actions. Any local UI feedback must become pending/confirmed/
failed state based on real backend/API results. Use legacy WPF/MVVM screens only
as parity reference, not as the target runtime. Reopen design only for concrete
defects found with real staging data or real operator flows.

Fresh workstation setup:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' checkout codex/operator-app-redesign
cd D:\projects\afk4.net\src\AFK4.Operator.App.Web
& 'C:\Program Files\nodejs\npm.cmd' ci
& 'C:\Program Files\nodejs\npm.cmd' test
& 'C:\Program Files\nodejs\npm.cmd' run build
cd D:\projects\afk4.net
& 'C:\Program Files\dotnet\dotnet.exe' restore AFK4.sln -p:NuGetAudit=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests\AFK4.Operator.App.Tests\AFK4.Operator.App.Tests.csproj --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

To open the current desktop app locally after build:

```powershell
$env:AFK4_OPERATOR_PLATFORM_BASE_URL = 'https://afk4.staging.mubi.dev'
$env:AFK4_OPERATOR_CURRENCY_CODE = 'TJS'
Start-Process -FilePath 'D:\projects\afk4.net\src\AFK4.Operator.App\bin\Debug\net10.0-windows\AFK4.Operator.App.exe' -WorkingDirectory 'D:\projects\afk4.net\src\AFK4.Operator.App\bin\Debug\net10.0-windows'
```

Next implementation order:

1. Staging-smoke POS, Clients, Payments, Logs, and Settings against
   `https://afk4.staging.mubi.dev`, then close parity gaps found with real data.

## Backend Connectivity TODO From Current React UI Copy

This checklist tracks every current Operator React surface that still tells the
operator, directly or indirectly, that the action is fixture-only, fallback-only,
or missing backend wiring. For each item, first check whether a Platform API
contract already exists. If it exists, wire the UI to that contract with
pending/confirmed/failed states and tests. If no backend contract exists, add
the backend endpoint/DTO/domain tests first, then wire the UI and remove the
missing-backend copy.

- [ ] Global fallback copy: remove production-visible `Fixture`,
  `Fixture fallback`, `SmartShell-like fixture`, and
  `Функция пока не подключена к backend.` states from signed-in operator flows.
  Keep fixture data only as a deliberate browser-dev/no-backend fallback behind
  clear dev-only state; production/staging should show backend loading, empty,
  or actionable error states.
- [x] Dashboard: replace fixture KPI values, focus queue, transition cards,
  period-driven synthetic metrics, and dashboard export feedback with backend
  dashboard/report read models. If no dashboard summary API exists, create
  endpoints for active shift summary, revenue/utilization, service/task queue,
  recent payments, and operator alert pressure.
  Implemented on 2026-05-21 with
  `GET /api/branches/{branchId}/dashboard/summary`, shared dashboard DTOs,
  Platform API endpoint tests, frontend route tests, and React Dashboard
  loading/confirmed/failed state.
- [ ] Map: wire `Техрежим`. Billing-mode
  selection beyond fast guest, registered-player/package selection for session
  start/extend, and selected-seat Agent/device command result feedback were
  implemented on 2026-05-21 against existing session, player/tariff/package,
  and device command status contracts. Problem/free/active/offline filters and
  table view parity were implemented on 2026-05-21 over backend-loaded seat
  state.
- [x] Booking: replace `Backend-контракт бронирований ещё не реализован.`,
  `booking API отсутствует`, `нет backend источника заявок`, `черновик ждёт
  backend booking API`, and `нет booking API` copy with real reservation
  functionality. If the backend still has no reservation module, create it:
  branch-scoped reservations, online requests, availability/conflict checks,
  create/edit/seat/move/cancel flows, permissions, audit records, and realtime
  updates.
  Implemented on 2026-05-21 with shared reservation DTOs, EF reservation
  persistence/migration, branch-scoped reservation endpoints, permission
  checks, audit records, overlap/session conflict checks, frontend route tests,
  Booking search/create/confirm/seat/update/cancel wiring, and client-card
  reservation creation. First-pass edge hardening added on 2026-05-21: mutation
  controls require `reservations.manage`, terminal/empty reservation states
  disable invalid actions, selected index is reset after reloads, and new
  reservations validate free-seat availability plus start time before API calls.
- [ ] POS: wire every quick operation that still uses local feedback or
  handoff copy: select customer for cart, refund by selected backend sale,
  void sale, receipt detail/print/export, wallet top-up handoff, new customer
  handoff, cash movement, stock write-off/adjustment, and discount/promo/combo
  handling if kept in MVP UI. Use existing POS/player/shift endpoints where
  they exist; create missing refund/void/receipt/inventory/provider endpoints
  before removing the UI warnings.
- [ ] Clients: wire `Создать бронь` from a selected player instead of throwing
  the missing booking-contract error. Also replace remaining local-only client
  surfaces with backend reads/actions: profile details/edit, purchase history,
  comments, groups, restrictions/discounts, package purchase/bonus operations,
  and privacy/audit-sensitive details. Create backend contracts where these do
  not already exist.
- [ ] Payments/Shifts: `Подготовить закрытие` now calls the real close-shift
  workflow with counted cash, notes, `shifts.close` permission gating, and
  backend confirmation. Cash movement creation now calls the real
  `/api/shifts/{shiftId}/cash-movements` endpoint with `shifts.cash.manage`
  permission gating. Remaining gaps include richer discrepancy handling,
  pending post-payment resolution, cancellation/refund actions where allowed,
  and selected operation detail. Use existing shift/report/POS contracts first;
  create missing payment-operation detail or approval endpoints if needed.
- [ ] Logs: replace projected-only event rows with backend-backed event detail,
  period/category/operator/target filters, correlation IDs, support handoff
  data, and export generation. Use audit, diagnostics, and report endpoints
  where they exist; create missing event-detail/support-export contracts if the
  current API cannot answer the screen.
- [ ] Settings: continue replacing local-only settings actions with real flows.
  Branch profile name/city save and general staff creation were implemented on
  2026-05-21 using backend profile/staff APIs plus existing permissions. POS
  category/product creation from `POS и склад` was implemented on 2026-05-21
  through existing POS catalog endpoints with `pos.catalog.manage` gating,
  price/stock form fields, and idempotency keys.
  Remaining settings gaps include role assignment/editing, general layout
  editor, device-seat management, tariff/package management, POS catalog edit/
  deactivate and stock CRUD, integrations/payment-provider settings, update
  rollout controls, diagnostics, audit/security settings, and validation
  errors.
- [ ] Empty backend states: review `Нет backend операций`,
  `Нет backend событий`, and similar empty-state copy after real staging data
  smoke. Keep them only when the backend returned a successful empty result;
  otherwise show actionable loading/error/retry state.
- [ ] Dev-only host state: keep `Native host bridge is unavailable.` as a
  browser-dev/WebView2-smoke diagnostic only. It should not appear in a normal
  packaged Operator App session.

Preserve the accepted fixture design baseline during this work; do not start
another broad fixture-only polish pass without a concrete staging or real-data
defect.

## Non-Negotiables

- Operator App remains a native Windows desktop app, not a web admin panel.
- The backend remains authoritative for sessions, money, POS, shifts, devices,
  and critical actions.
- Critical actions continue to wait for backend API confirmation.
- Protected token storage remains native-side; frontend tokens must not be
  persisted in browser localStorage.
- Normal operator paths must avoid raw GUID/form surfaces.
- `AFK4_OPERATOR_PLATFORM_BASE_URL` and `AFK4_OPERATOR_CURRENCY_CODE` remain
  supported for local and staging smoke.
- Existing backend APIs and shared DTO intent remain the contract baseline.

## Target Structure

- `src/AFK4.Operator.App/`
  - .NET desktop host, WebView2 startup, host bridge, packaging entrypoint.
- `src/AFK4.Operator.App.Web/`
  - React/TypeScript frontend, Vite config, API clients, state stores, views,
    component tests.
- `tests/AFK4.Operator.App.Tests/`
  - host configuration, protected storage, bridge, WebView2 asset resolution,
    and packaging invariant tests.
- `tests/AFK4.Operator.App.Web.Tests/` or frontend test config under
  `src/AFK4.Operator.App.Web/`
  - component/state/API tests.

## Task 1: Host Shell Skeleton

- [x] Add WebView2 package to `AFK4.Operator.App`.
- [x] Replace the WPF visual tree with a minimal native window that initializes
  WebView2 and loads local built frontend assets.
- [x] Preserve app title, minimum window size, app configuration, and staging
  environment variable behavior.
- [x] Add host tests for base URL/currency config and local asset path
  resolution.

## Task 2: React/TypeScript Frontend Foundation

- [x] Add `src/AFK4.Operator.App.Web` with Vite, React, TypeScript, test
  scripts, and production build output.
- [x] Add a typed configuration bootstrap from the host to the frontend.
- [x] Add frontend API client boundary for auth.
- [x] Add frontend API client boundaries for floor map, sessions, POS, players,
  shifts/reports, settings/pilot setup, devices, diagnostics, updates, and
  audit.
- [x] Add frontend tests for config bootstrap and first workspace rendering.
- [x] Add frontend tests for API error projection.

## Task 3: Auth And Token Boundary

- [x] Keep staff sign-in request flow compatible with existing backend auth.
- [x] Store refresh/access token material through native protected storage.
- [x] Expose only narrow host bridge methods needed for token retrieval,
  refresh, and sign-out.
- [ ] Add host bridge app diagnostics if a concrete operator/support need
  appears.
- [x] Add tests proving tokens are not persisted in browser storage.

## Frontend/Design Roadmap After Backend Wiring

After SignalR state, backend-backed floor-map loading, and backend-confirmed
selected-seat actions are wired, bring the React operator screens to
production parity in this order. The reference workflow set is the public
SmartShell admin/operator structure:
Dashboard, Gaming stations/Map, Booking, Shop, Payments, Clients, Logs, and
Settings.

Design rules for this phase:

- Keep the Operator App as a native Windows desktop app with local WebView2
  assets, not a browser-delivered web admin panel.
- Keep the floor map as AFK4's first screen even though SmartShell has a
  Dashboard entry; Dashboard becomes a secondary operational summary.
- Use restrained status color: neutral dark surfaces, one selected/primary blue,
  green only for healthy active state, amber/red only for required attention,
  and gray for offline/service/neutral states.
- Keep all critical session, money, POS, device, update, and settings commands
  blocked on backend confirmation.
- Treat any raw GUID/form-first path in normal cashier/operator workflows as a
  UI defect unless it is an advanced technician/debug surface.

Screen roadmap:

1. **Map and gaming-station table:** real floor data, real selected-seat
   actions, status filters, multi-select, booking context, technical mode entry,
   device status, Shell status, and command/result states.
2. **Dashboard:** active shift summary, revenue, active users, task/service
   queue, license/account pressure, recent payments, and quick actions that
   deep-link into the exact operator workflow.
3. **Booking:** active reservations, online requests, strict reservation state,
   create/edit/cancel flows, seat availability, and reservation conflict
   messages.
4. **Shop/POS:** customer lookup, product/service/combo catalog, cart,
   discount/promo code, payment method, stock add/write-off, refund/void, and
   receipt state.
5. **Payments/shift:** current shift transactions, pending post-payments,
   cancellation where allowed, cash movements, close-shift reconciliation, and
   exports.
6. **Clients:** client search by nickname/phone, deposit/debt/discount widgets,
   profile, purchase history, comments, groups, top-up, and debt payment.
7. **Logs:** current-shift/all-events toggle, period/category filters, universal
   search across client/item/computer/operator, and action history detail.
8. **Settings:** club profile, staff/roles, layout, devices, tariffs, POS
   catalog, integrations, diagnostics, updates, audit, and security settings
   grouped for an owner/admin setting up their club.

## Task 4: Floor Map Concept UI

- [x] Implement the accepted concept as the first screen with local UI fixtures:
  dark top command bar, dark left rail, dense floor map, status metrics,
  filters, operational signals, and right selected-seat panel.
- [x] Map documented status tones: ready, active, pending, warning, blocking,
  offline, and service.
- [x] Load the primary floor map from the backend after native staff auth, with
  fixture fallback for browser-dev/no-backend runs.
- [x] Cover floor-map DTO state mapping and device-status overlay behavior with
  frontend tests.
- [x] Preserve selected-seat actions: fast guest start, extend +15/+30,
  transfer, end, and backend confirmation for backend-loaded seats.
- [x] Add billing-mode selection beyond fast guest and Agent/device command
  result status. Implemented on 2026-05-21 with guest/prepaid/package/postpaid
  map controls, backend player/tariff/package selection, billed start/extend
  request payloads, command-status feedback, and frontend coverage for fast
  guest plus prepaid-wallet start.
- [x] Cover problem filters after backend-backed filter state replaces the
  fixture-only implementation. Implemented on 2026-05-21 with all/free/active/
  attention/offline filters and a table view backed by the same `SeatSummary`
  data as the map tiles.

## Task 5: POS, Players, Shift, And Settings Parity

- [x] Add local SmartShell-inspired fixture workspaces for Dashboard, Booking,
  POS/Shop, Clients, Payments, Logs, and Settings so the visual direction
  and navigation are reviewable before backend parity.
- [x] Add fixture-level interactivity and motion across the reviewable React
  workspaces, including local selection/filter/cart/payment/settings state,
  action feedback notices, animated values, and reduced-motion support.
- [ ] Port current POS product/cart/payment/refund/void workflows.
- [ ] Port player search, wallet/debt, top-up, and debt payment workflows.
- [ ] Port shift open, cash movement, close, reports, and CSV export flows.
- [ ] Port Settings/Pilot Setup, device tools, updates, audit, and diagnostics
  enough to preserve current operator capability.

## Task 6: Realtime State

- [x] Add SignalR JavaScript client for floor map/device updates.
- [x] Keep realtime updates as context only; critical action success is still
  based on backend API responses.
- [x] Cover disconnected/reconnecting/connected client states.
- [ ] Add session-specific realtime events if backend contracts expose them.

## Task 7: Packaging And CI

- [ ] Update `scripts/build-client-packages.ps1` to build frontend assets before
  Operator App publish and include them in the Operator App MSI.
- [ ] Add WebView2 Runtime prerequisite/bootstrap handling to the Operator App
  installer.
- [ ] Update package smoke tests for the frontend build output and MSI content.
- [ ] Keep update package component name `operator-app`.

## Task 8: Cutover And Cleanup

- [ ] Remove WPF screen/view-model code only after WebView2/React parity is
  verified for the pilot day flow.
- [ ] Keep backend contracts and Agent/Player Shell unchanged unless a specific
  parity gap requires a separate approved change.
- [ ] Run Operator App against staging and record sign-in, floor map, session
  actions, POS/shift basics, Pilot Setup, and actionable error evidence.
- [ ] Update progress and roadmap with final verification.

## Verification Gates

- [x] Host tests pass.
- [x] Frontend tests pass.
- [x] Full `dotnet test AFK4.sln` passes.
- [x] Operator App builds and opens to the React UI locally.
- [ ] Operator App can target `https://afk4.staging.mubi.dev`.
- [ ] Operator App MSI includes host binaries, built frontend assets, and WebView2
  prerequisite handling.
