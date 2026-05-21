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
- The current UI is still fixture-backed. Backend auth, protected token bridge,
  typed API clients, SignalR, permissions, and backend-confirmed critical
  actions are the next real implementation work.
- Legacy WPF screens/ViewModels remain in the repository as parity reference
  until the WebView2/React day flow covers pilot operations.

Next-session kickoff:

The design phase is done. Do not reopen broad fixture polish. The accepted
baseline is the current WebView2/React Operator UI: Map as the primary
workspace, plus Dashboard, Booking, POS, Clients, Payments, Logs, and Settings
with the reviewed dense operator layout, Russian-first copy, local
interactions, action feedback, animated values, and reduced-motion support.

Start the next session by turning this fixture UI into a real operator app
without changing the approved visual direction. The next work is engineering:
native protected-token bridge and staff sign-in, typed frontend API clients,
common error projection, SignalR realtime state, and backend-confirmed
floor-map actions. After that, replace fixture state with backend-backed
behavior for POS, clients, payments, logs, settings, shifts, diagnostics,
updates, audit, and reports.

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

1. Native protected token bridge and staff sign-in.
2. Typed frontend API clients and common error projection.
3. SignalR realtime state for floor map/device/session updates.
4. Real floor-map actions with backend-confirmed start, extend, transfer, and
   stop flows.
5. Backend-backed parity for Dashboard, Booking, POS/Shop, Clients, Payments,
   Logs, and Settings following the roadmap below.

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
- [ ] Add frontend API client boundaries for auth, floor map, sessions, POS,
  players, shifts, settings, updates, audit, and diagnostics.
- [x] Add frontend tests for config bootstrap and first workspace rendering.
- [ ] Add frontend tests for API error projection.

## Task 3: Auth And Token Boundary

- [ ] Keep staff sign-in request flow compatible with existing backend auth.
- [ ] Store refresh/access token material through native protected storage.
- [ ] Expose only narrow host bridge methods needed for token retrieval,
  refresh, sign-out, and app diagnostics.
- [ ] Add tests proving tokens are not persisted in browser storage.

## Frontend/Design Roadmap After Backend Wiring

After the auth/token boundary, typed API clients, and SignalR state are wired,
bring the React operator screens to production parity in this order. The
reference workflow set is the public SmartShell admin/operator structure:
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
- [ ] Preserve selected-seat actions: start, extend +15/+30, transfer, end,
  billing mode, backend confirmation, and device command status.
- [ ] Cover state mapping and problem filters with frontend tests.

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

- [ ] Add SignalR JavaScript client for floor map/device/session updates.
- [ ] Keep realtime updates as context only; critical action success is still
  based on backend API responses.
- [ ] Cover disconnected/reconnecting/connected UI states.

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
