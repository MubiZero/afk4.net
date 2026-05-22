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
  for mutation controls. Settings now has backend-backed staff creation and
  predefined branch-role reassignment in the Personnel section, branch profile
  save backed by a new Platform API
  profile endpoint, POS category/product creation and product update/deactivation
  backed by the existing POS catalog model, stock movement creation backed by
  the existing inventory endpoint, stock movement history backed by the existing
  inventory read endpoint, tariff/version creation backed by the existing tariff
  endpoints, and package definition creation/update/deactivation backed by the
  existing package endpoint. Payments can now open a shift through the existing
  open-shift endpoint, close the current shift through the existing close-shift
  endpoint with counted cash and a closing note, and can record cash movements
  through the existing shift cash movement endpoint. Settings `Интеграции` now
  registers update packages, creates rollouts, shows selected rollout status/
  device snapshots, and changes package/rollout state through the existing
  update endpoints. Logs now refreshes backend audit
  records through action/outcome/target type/date range/limit filters and shows
  selected audit/diagnostics event detail from the loaded backend rows; source
  cards now filter loaded events by all/Agent/POS/Operator/Platform; period
  presets now execute audit searches for today, the last 24 hours, or the last
  7 days; export buttons now download backend operator-action/shift CSV files
  and local audit/error JSON bundles from loaded audit/diagnostics data.
  Settings `Залы и
  ПК` now creates/updates layout zones/seats, creates enrollment codes, assigns device
  ids to seats, and reads device detail with status/version/credential/app
  counts plus recent command history through existing layout/device endpoints,
  including credential rotation/revocation controls. POS
  quick refund now calls the existing refund endpoint for the selected backend
  sale, and POS draft void creates a backend draft from the current cart before
  calling the existing void endpoint. POS recent receipt rows now open backend
  sale detail through the existing sale lookup endpoint and use the sale's
  latest receipt id to read `GET /api/receipts/{receiptId}` for receipt
  number/type/total display; loaded receipts can now be printed or exported
  locally from the POS detail panel. POS cart customer lookup now searches backend
  players and sends nullable `playerAccountId` through the POS sale contract,
  with Platform API persistence/projection and EF migration coverage. POS cart
  new-customer creation now posts to the existing branch player API and
  immediately selects the created player for checkout. POS quick deposit
  top-up now posts the selected cart client's current cart total to the
  existing wallet top-up endpoint with `billing.wallet.top_up` gating. POS
  quick stock write-off now records an inventory stock movement through the
  existing stock-movement endpoint with `inventory.stock.manage` gating. Clients package
  purchase now calls the existing package option and purchase endpoints for the
  selected backend player, the selected client profile now reads active player
  packages through the existing player packages endpoint, and wallet top-up now uses an operator amount/reason
  form before calling the existing top-up endpoint. Debt payment now also uses
  an operator amount/reason form before calling the existing debt payment
  endpoint. Player creation now uses an operator name/phone form before calling
  the existing player creation endpoint. Staging smoke across the newly wired screens is the
  next real implementation work.
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
pressure, focus queue, recent payments, and export CSV download. Booking has real
reservation API wiring, restored staff permissions now gate workspace
navigation plus selected-seat session actions, the map panel now sends billed
session metadata plus displays device command status feedback, and the primary
map has real filter/table parity. Booking mutation controls now also respect
`reservations.manage` and selected reservation state. Settings Personnel can
create branch staff and reassign predefined branch roles through backend staff
APIs, and profile save
now updates branch name/city through the backend. Settings `POS и склад` can
also create a backend POS category and product, and Settings `Тарифы` can
create/update/deactivate backend tariffs and tariff versions plus
create/update/deactivate package definitions. Settings `POS и
склад` can also record inventory stock movements for tracked products, read
the latest stock movement history, and update/deactivate selected POS products.
Payments open-shift now calls
the backend open-shift endpoint with starting cash and opening note fields.
Payments close-shift now calls the backend close-shift endpoint with counted
cash and note fields, and Payments cash movement creation calls the backend
shift cash endpoint. Payments selected operation detail now uses already
loaded backend sales/cash report rows for id, shift, source, and line/reason
context. Payments report export buttons now download backend sales/cash/shift
CSV files plus a local discrepancy JSON. Settings `Интеграции` now exposes
backend-backed update package registration, rollout creation, selected rollout
status/device snapshot display, and package/rollout state changes with
the existing `updates.packages.manage` and `updates.rollouts.manage` guards.
Logs now applies backend audit action/outcome/target type/date range/limit
filters through the existing audit search endpoint and selected event detail
now uses loaded audit/diagnostics rows; source cards now filter the loaded
event list by all/Agent/POS/Operator/Platform; period presets now execute
audit searches for today, the last 24 hours, or the last 7 days; export buttons
now download backend operator-action/shift CSV files and local audit/error JSON
bundles from loaded audit/diagnostics data. Settings `Залы и ПК` now exposes
backend-backed layout zone/seat creation/update, enrollment-code creation,
device-to-seat assignment, and device detail lookup with status/version/
credential/app counts plus recent command history through the existing layout
and device permissions, plus credential rotation and revocation through the
existing credential lifecycle endpoints.
POS
quick refund calls the backend refund endpoint for the selected backend sale, and
POS draft void calls the backend void endpoint after creating a draft from the
current cart. POS recent receipt rows call the backend sale lookup endpoint for
line detail and then call the receipt lookup endpoint through the sale's
`latestReceipt` projection; loaded receipts can now be printed or exported
locally from the POS detail panel. POS cart customer lookup now searches backend
players and attaches the selected `playerAccountId` to checkout/draft sale
creation. POS cart new-customer creation now posts to the existing branch
player API and selects the created player for checkout. POS quick deposit
top-up now posts the selected cart client's current cart total to the
existing wallet top-up endpoint with `billing.wallet.top_up` gating. POS quick
stock write-off now records an inventory stock movement through the existing
stock-movement endpoint with `inventory.stock.manage` gating. Clients profile now reads active player
packages through the existing player packages endpoint. Clients package purchase calls the backend package purchase
endpoint for the selected player, and Clients wallet top-up sends
operator-entered amount/reason to the backend. Clients debt payment now also
sends operator-entered amount/reason to the backend with a no-overpayment guard.
Clients player creation now sends operator-entered name/phone to the backend.
Clients reservation creation from the selected backend player now remains
disabled unless the restored staff session includes `reservations.manage`.
Continue with staging smoke of the backend-backed workspaces and close gaps
found with real data.

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

- [x] Global fallback copy: remove production-visible `Fixture`,
  `Fixture fallback`, `SmartShell-like fixture`, and
  `Функция пока не подключена к backend.` states from signed-in operator flows.
  Keep fixture data only as a deliberate browser-dev/no-backend fallback behind
  clear dev-only state; production/staging should show backend loading, empty,
  or actionable error states.
  Implemented on 2026-05-21: signed-in status labels now distinguish
  `Backend live`, `Loading backend`, `Backend error`, and `Dev demo`; generic
  missing-contract feedback now says there is no backend contract instead of
  presenting fixture success.
- [x] Dashboard: replace fixture KPI values, focus queue, transition cards,
  period-driven synthetic metrics, and dashboard export feedback with backend
  dashboard/report read models. If no dashboard summary API exists, create
  endpoints for active shift summary, revenue/utilization, service/task queue,
  recent payments, and operator alert pressure.
  Implemented on 2026-05-21 with
  `GET /api/branches/{branchId}/dashboard/summary`, shared dashboard DTOs,
  Platform API endpoint tests, frontend route tests, and React Dashboard
  loading/confirmed/failed state. Dashboard export now downloads the backend
  sales CSV instead of only confirming the fetch.
- [x] Map: wire `Техрежим`. Billing-mode
  selection beyond fast guest, registered-player/package selection for session
  start/extend, and selected-seat Agent/device command result feedback were
  implemented on 2026-05-21 against existing session, player/tariff/package,
  and device command status contracts. Problem/free/active/offline filters and
  table view parity were implemented on 2026-05-21 over backend-loaded seat
  state.
  `Техрежим` was implemented on 2026-05-21 as a backend-confirmed selected-seat
  action that reads the selected device detail and branch diagnostics through
  existing contracts before showing a technician summary.
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
  handoff copy. Checkout already creates a backend sale/manual payment, and
  `Возврат по чеку` now calls the backend refund endpoint for the selected
  backend sale with `pos.sales.refund` gating. `Аннулировать черновик` now
  creates a backend draft sale from the current cart and voids it through the
  existing void endpoint with `pos.sales.void` gating. Recent receipt rows now
  open backend sale line detail and receipt number/type/total through
  `GET /api/pos/sales/{saleId}` plus `GET /api/receipts/{receiptId}`.
  Selected customer for cart was implemented on 2026-05-21 with backend player
  search, nullable `playerAccountId` on POS sale request/response/entity, EF
  migration, and checkout/draft create-sale wiring. New customer handoff was
  implemented on 2026-05-21 with POS-cart player creation through the existing
  branch player API and immediate checkout selection. POS stock write-off was
  implemented on 2026-05-21 through the existing inventory stock-movement
  endpoint with `inventory.stock.manage` gating, negative adjustment quantity,
  operator reason, and idempotency key. POS wallet top-up handoff was
  implemented on 2026-05-21 by posting the selected cart client's current cart
  total to the existing wallet top-up endpoint with `billing.wallet.top_up`
  gating and idempotency key. Receipt print/export was implemented on
  2026-05-21 from the loaded backend sale/receipt projection. Remaining POS
  gaps include discount/promo/combo handling if kept in MVP UI.
  Use existing POS/player/shift endpoints where they exist; create missing
  customer/cart/inventory/provider endpoints before removing the UI warnings.
- [ ] Clients: `Создать бронь` from a selected player, wallet top-up and debt
  payment with operator-entered amount/reason, player creation with
  operator-entered name/phone, and package purchase now use backend endpoints.
  Reservation creation from the selected backend player now also respects
  `reservations.manage` in the UI.
  Selected client active package detail now reads the existing player packages
  endpoint and renders package name/minutes/state in the profile. Remaining
  local-only client surfaces include richer profile edit, purchase history,
  comments, groups, restrictions/discounts, package bonus operations, and
  privacy/audit-sensitive details. Create backend contracts where these do not
  already exist.
- [ ] Payments/Shifts: `Открыть смену` now calls the real open-shift workflow
  with starting cash, opening note, `shifts.open` permission gating, and backend
  confirmation when no current shift exists. `Подготовить закрытие` now calls
  the real close-shift workflow with counted cash, notes, `shifts.close`
  permission gating, and backend confirmation. Cash movement creation now calls
  the real `/api/shifts/{shiftId}/cash-movements` endpoint with
  `shifts.cash.manage` permission gating. Selected operation detail now uses
  the already loaded backend sales/cash report row for id, shift, source, and
  line/reason context. Payments report export buttons now download backend
  sales/cash/shift CSV files plus a local discrepancy JSON. Remaining gaps
  include richer discrepancy handling, pending post-payment resolution, and
  cancellation/refund actions where allowed. Use existing shift/report/POS
  contracts first; create missing payment-operation detail or approval
  endpoints if needed.
- [ ] Logs: backend audit action/outcome/target type/date range/limit filters were added
  on 2026-05-21 through the existing audit search endpoint. Selected event
  detail from already loaded audit rows and diagnostics command/update/stale
  rows was added on 2026-05-21, and source cards now filter loaded events by
  all/Agent/POS/Operator/Platform. Operator period presets for today, the last
  24 hours, and the last 7 days were added on 2026-05-21. Export downloads for
  backend operator-action/shift CSV plus local audit/error JSON bundles were
  added on 2026-05-21. Remaining Logs gaps are correlation IDs and richer
  support handoff data. Use audit, diagnostics, and report endpoints where
  they exist; create missing support-export contracts if the current API cannot
  answer the screen.
- [ ] Settings: continue replacing local-only settings actions with real flows.
  Branch profile name/city save and general staff creation were implemented on
  2026-05-21 using backend profile/staff APIs plus existing permissions.
  Predefined branch-role reassignment from Personnel was implemented on
  2026-05-22 through
  `PATCH /api/branches/{branchId}/staff/{staffUserId}/roles` with
  `identity.roles.manage` gating. POS
  category/product creation from `POS и склад` was implemented on 2026-05-21
  through existing POS catalog endpoints with `pos.catalog.manage` gating,
  price/stock form fields, and idempotency keys. POS product
  update/deactivation from `POS и склад` was implemented on 2026-05-22 through
  `PATCH /api/branches/{branchId}/pos/products/{productId}` with
  `pos.catalog.manage` gating. Tariff/version creation from
  `Тарифы` was implemented on 2026-05-21 through the existing tariff endpoints
  with `tariffs.manage` gating, hourly price, minimum/rounding fields, and
  idempotency keys. Package definition creation from `Тарифы` was implemented
  on 2026-05-21 through the existing package endpoint with `packages.manage`
  gating, minute/bonus/expiry fields, and idempotency keys. Package definition
  update/deactivation from `Тарифы` was implemented on 2026-05-22 through
  `PATCH /api/branches/{branchId}/packages/{packageDefinitionId}` with
  `packages.manage` gating. Inventory stock
  movement creation from `POS и склад` was
  implemented on 2026-05-21 through the existing stock-movement endpoint with
  `inventory.stock.manage` gating and idempotency keys. Read-only stock
  movement history from `POS и склад` was implemented on 2026-05-22 through
  `GET /api/branches/{branchId}/inventory/stock-movements` with
  `inventory.view` gating, product filtering, and a bounded limit. Update package
  registration, rollout creation, and package/rollout state changes from
  `Интеграции` were implemented on 2026-05-21 through the existing update
  endpoints with `updates.packages.manage` and `updates.rollouts.manage`
  gating; selected rollout status/device snapshot display was added on
  2026-05-22. Device setup from `Залы и ПК` was implemented on 2026-05-21 through
  the existing enrollment-code, device-seat assignment, and device detail
  endpoints with existing device permission gating. The same section now has a
  general layout form for zone/seat creation and selected zone/seat update
  through layout endpoints with `layout.manage` gating. Credential rotation and revocation were
  added to the same section on 2026-05-21 through the existing credential
  lifecycle endpoints.
  Staff activation/deactivation and password reset from `Персонал` were
  implemented on 2026-05-22 through
  `PATCH /api/branches/{branchId}/staff/{staffUserId}/state` and
  `POST /api/branches/{branchId}/staff/{staffUserId}/password-reset` with
  `identity.branch_staff.manage` gating, audit records, and active staff token
  revocation on deactivation/password reset.
  Tariff update/deactivation from `Тарифы` was implemented on 2026-05-22
  through `PATCH /api/branches/{branchId}/tariffs/{tariffId}` and
  `PATCH /api/branches/{branchId}/tariffs/{tariffId}/versions/{tariffVersionId}`
  with `tariffs.manage` gating, audit records, active option removal after
  deactivation, and material-change rejection when a tariff version is already
  used by sessions.
  Layout update/reorder from `Залы и ПК` was implemented on 2026-05-22
  through `PATCH /api/branches/{branchId}/layout/zones/{zoneId}` and
  `PATCH /api/branches/{branchId}/layout/seats/{seatId}` with `layout.manage`
  gating, audit records, duplicate-name checks, and seat moves between zones.
  Settings device detail display was expanded on 2026-05-22 to show online/
  locked state, seat/zone placement, heartbeat, Agent/Shell versions,
  credential/app counts, and recent command status/message rows.
  Settings rollout detail display was expanded on 2026-05-22 to show selected
  rollout state, target, batch, channel, package, timing, device count, and
  device update status/message snapshots from `UpdateRolloutStatusDto`.
  Remaining settings gaps include custom roles/arbitrary permission-set editing,
  staff profile detail, richer visual/archive layout editing beyond name/sort/
  zone updates,
  fleet-level device inventory/history management, package purchase UX refinements,
  advanced POS catalog management, advanced inventory
  controls and reconciliation, integrations/payment-provider
  settings, richer rollout filtering/history controls, diagnostics,
  audit/security settings, and
  validation errors.
- [x] Empty backend states: review `Нет backend операций`,
  `Нет backend событий`, and similar empty-state copy after real staging data
  smoke. Keep them only when the backend returned a successful empty result;
  otherwise show actionable loading/error/retry state.
  Implemented on 2026-05-21 for Payments and Logs: successful empty report/audit
  responses now show period-specific empty copy, loading and failed states have
  separate placeholder rows, and search/filter misses have their own no-match
  copy.
- [x] Dev-only host state: keep `Native host bridge is unavailable.` as a
  browser-dev/WebView2-smoke diagnostic only. It should not appear in a normal
  packaged Operator App session.
  Implemented on 2026-05-21: host bridge availability failures are now typed,
  packaged `webview2` auth errors project to operator-facing restart/check-host
  copy, and browser-dev still exposes the exact diagnostic for local smoke.

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
- `AFK4_OPERATOR_PLATFORM_BASE_URL`, `AFK4_OPERATOR_ORGANIZATION_ID`,
  `AFK4_OPERATOR_BRANCH_ID`, and `AFK4_OPERATOR_CURRENCY_CODE` remain supported
  for local and staging smoke.
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
- [x] Port current POS product/cart/payment/refund/void workflows. Implemented
  on 2026-05-21 with backend catalog/current-shift/sales report reads, sale
  creation, manual payment, selected backend customer attribution,
  POS-cart new-customer creation, selected-sale refund, draft void, sale
  detail, receipt lookup, receipt print/export, quick wallet top-up, and quick stock write-off
  through existing POS/player/receipt/inventory endpoints.
- [ ] Port player search, wallet/debt, top-up, and debt payment workflows.
- [ ] Port shift open, cash movement, close, reports, and CSV export flows.
- [x] Port Settings/Pilot Setup, device tools, updates, audit, and diagnostics
  enough to preserve current operator capability. Implemented on 2026-05-21
  with branch profile, staff, layout, tariff/package, POS/inventory, updates,
  audit/diagnostics, enrollment, seat assignment, device detail, command
  dispatch, credential lifecycle, stock movement history, POS product
  update/deactivation, and package definition update/deactivation wiring
  through existing backend endpoints.

## Task 6: Realtime State

- [x] Add SignalR JavaScript client for floor map/device updates.
- [x] Keep realtime updates as context only; critical action success is still
  based on backend API responses.
- [x] Cover disconnected/reconnecting/connected client states.
- [ ] Add session-specific realtime events if backend contracts expose them.

## Task 7: Packaging And CI

- [x] Update `scripts/build-client-packages.ps1` to build frontend assets before
  Operator App publish and include them in the Operator App MSI. Implemented on
  2026-05-21 with `npm ci`, `npm run build`, Vite `dist` copy into published
  `WebAssets`, Node setup in client package workflows, and a local
  `0.1.999-internal` package build that produced Operator/Gaming PC MSI
  artifacts.
- [x] Add WebView2 Runtime prerequisite/bootstrap handling to the Operator App
  installer. Implemented as an MSI launch condition on 2026-05-21 using
  WebView2 Evergreen Runtime HKLM/HKCU EdgeUpdate `pv` registry searches and a
  clear first-install prerequisite message; no offline runtime bootstrapper is
  bundled yet.
- [x] Add automated MSI content assertion for built frontend assets after WiX
  output. Implemented on 2026-05-21 by reading the Operator MSI File table after
  WiX build and requiring frontend `index.html`, JavaScript, and CSS entries.
- [x] Keep update package component name `operator-app`.

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
- [x] Local Operator App package build includes built React frontend assets in
  published `WebAssets`.
- [x] Operator App can target `https://afk4.staging.mubi.dev` through native
  host environment configuration; focused option tests and staging health check
  passed on 2026-05-21.
- [x] Operator App MSI includes host binaries, built frontend assets, and WebView2
  prerequisite handling.
