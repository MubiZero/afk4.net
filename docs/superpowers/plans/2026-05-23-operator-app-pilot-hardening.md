# Operator App Technical-Surface Cleanup And Hardening Plan

Status: active follow-up for `codex/operator-app-redesign`

Implemented slices:

- 2026-05-23: removed signed-in POS/Players fixture leakage for authoritative
  empty backend responses and restricted the WebView2 dev-server URL to
  localhost/loopback origins.
- 2026-05-23: added first critical-action confirmation guards for session end,
  POS refund/void with visible reason, shift close, and device credential
  revoke.
- 2026-05-23: completed the planned critical-action confirmation set by adding
  two-step confirmations for layout zone/seat deletion and update package/
  rollout state changes.
- 2026-05-28: removed the normal Operator App sign-in organization GUID field
  and replaced Settings device/update raw-ID entry with operator-facing device,
  package, and rollout selections.

## Purpose

This plan turns the current Operator App critique into actionable cleanup and
hardening work. It does not reopen the approved product or architecture
decisions: AFK4 remains a native Windows Operator App with a WebView2 shell and
React/TypeScript UI, backend-authoritative critical actions, no browser web
admin as the primary club UI, and no local club server.

The near-term goal is to remove harmful technical surfaces from normal
operator work before broadening smoke coverage: sign in, open/observe shift,
use the floor map, start/extend/end sessions, handle POS, manage players,
inspect payments/shifts, use logs, and perform setup/device/update operations
without demo data, raw IDs, backend-shaped forms, or ambiguous critical
actions leaking into the day flow.

## Problems To Fix

1. Staging evidence is still incomplete for the backend-backed React
   workspaces. Unit and local build checks are not enough for release
   confidence.
2. Demo/fixture state must not appear as real operational data after staff
   sign-in. Empty backend catalog, player, report, or device responses must be
   shown as explicit empty states, not replaced by demo data.
3. The raw organization GUID has been removed from normal sign-in, and the
   first Settings device/update raw-ID pass is complete. Remaining setup,
   support, and diagnostics surfaces still need a pass for technical keys,
   backend-shaped forms, and unclear operator copy.
4. Critical actions need stronger operator-safe UX: confirmation, reason where
   required, visible money/session/device impact, pending backend state, and
   final backend result.
5. Normal cashier/operator copy still contains too much technical language in
   some paths. Agent/backend details belong in technician or diagnostics
   surfaces.
6. The React frontend is too concentrated in one large `App.tsx` and one large
   stylesheet. This increases regression risk for money, shift, and session
   changes.
7. Several frontend API response types are still too loose for operational
   workflows and rely on defensive field readers instead of typed contracts.
8. The WebView2 development-server bridge should fail closed outside local
   development so token-bearing host messages cannot be exposed to an arbitrary
   remote origin.
9. Responsive behavior needs evidence at the current native minimum window
   size and the expected operator desktop size.
10. React hotkeys have not yet reached the legacy WPF operator-speed baseline.

## Work Order

### 1. Remove Production Demo Leakage

- Keep fixture data only for deliberate browser-dev/no-backend demos.
- After staff sign-in, show backend loading, empty, failed, or permission
  states. Do not silently replace empty backend responses with demo products,
  players, receipts, reports, shifts, devices, or settings.
- Clear fixture carts/selections when the backend response is authoritative and
  empty.

Acceptance criteria:

- A signed-in POS screen with an empty backend catalog shows an empty catalog
  state and disables checkout instead of showing demo products.
- A signed-in Players screen with no backend matches shows an empty result and
  no selected demo player.
- Tests cover empty backend catalog/player responses.

### 2. Harden WebView2 Host Boundary

- Restrict `AFK4_OPERATOR_WEB_DEV_SERVER_URL` to loopback/local development
  origins unless a deliberate debug-only override is introduced.
- Ensure packaged/staging shell runs only built local assets from
  `operator.afk4.local`.
- Keep protected token storage native-side and continue rejecting browser
  storage persistence.

Acceptance criteria:

- Host option tests reject non-loopback dev-server URLs.
- Existing local Vite dev/preview URLs remain supported for development.

### 3. Staging Smoke The Backend-Backed Workspaces

- Run the WebView2/React Operator App against
  `https://afk4.staging.mubi.dev`.
- Smoke sign-in, floor map, session start/extend/end, POS empty and normal
  catalog states, player search/create/top-up/debt, payments open/close/cash
  movement/export, logs filters/detail/export, and settings setup/device/update
  paths.
- Record concrete findings in the progress snapshot.

Acceptance criteria:

- Progress docs name the staging user/role used, date, tested workspaces,
  passed workflows, failed workflows, and follow-up branch/issues.

### 4. Add Critical-Action UX Guards

- Add confirmation surfaces for session end, POS refund/void, shift close,
  device credential revocation, layout deletion, and update rollout state
  changes.
- Require a visible reason where backend or audit policy requires it.
- Show expected money/session/device impact before dispatching.

Acceptance criteria:

- Critical buttons do not send API requests on first accidental click.
- Tests cover at least session end, POS refund, shift close, and credential
  revoke confirmations.

### 5. De-Technicalize Normal Operator Copy

- Replace English/technical status copy in the primary operator path with
  Russian-first operational copy.
- Keep raw device command names, ids, and backend diagnostics in technician,
  audit, or support surfaces only.

Acceptance criteria:

- Floor map and selected-seat panel no longer show `No route`, `Lease fresh`,
  `Device unassigned`, raw command names, or raw ids in the cashier path.

### 6. Split The React Frontend By Feature

- Extract shell/auth/floor-map/POS/players/payments/logs/settings into feature
  modules.
- Move shared primitives, formatting helpers, API adapters, and fixture/demo
  data into separate files.
- Keep edits behavior-preserving while splitting.

Acceptance criteria:

- `App.tsx` owns application composition only and is small enough to review.
- Feature modules have focused tests.

### 7. Strengthen Frontend Contracts

- Replace `Record<string, unknown>` DTOs with typed contracts for money,
  shifts, POS sales/receipts, players, packages, reports, audit, diagnostics,
  devices, and update rollout summaries where UI logic depends on fields.
- Keep DTO naming aligned with `AFK4.Shared.Contracts`.

Acceptance criteria:

- Critical money/session/shift UI code no longer relies on stringly field
  readers for required fields.

### 8. Restore Production-Speed Hotkeys

- Port React hotkeys for map focus, search, POS, players, payments, settings,
  extend session, end session, and dismiss transient state.
- Avoid triggering destructive actions without the confirmation guards above.

Acceptance criteria:

- Hotkey tests cover workspace navigation and selected-seat safe actions.

## First Implementation Slice

Start with the smallest high-risk fix:

1. Remove signed-in POS and Players fixture leakage for empty backend
   responses.
2. Add focused frontend tests for empty backend POS catalog and empty player
   search.
3. Restrict the WebView2 dev-server URL to localhost/loopback with host tests.

This slice reduces operational risk without changing product scope or backend
contracts.
