# Frontend Consolidation — One Operator Surface, Shared Web Libraries (Architecture Decision)

- **Date:** 2026-06-01
- **Status:** Design (architecture decision + migration spec; pending founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[operator-app-webview2-react-migration]] (in-flight),
  [[platform-counter-loop-postpaid-checkout-design]],
  [[platform-web-redesign]], realtime-consistency spec (companion),
  localization spec (companion)

> This is an **architecture decision and migration spec**, not a feature. It picks the
> *target frontend topology* for the product and the *path to get there* from today's
> partially-migrated state. It does not add user-facing capability; it removes
> duplication, defines what is native vs web, and establishes a shared-library strategy
> so the operator ergonomics work in the counter-loop / realtime / localization specs is
> built **once**, on the right surface.

## 1. Context & Problem

The product ships **three frontends**, two of which overlap heavily:

| Surface | Tech | Role | Size signal |
|---|---|---|---|
| `AFK4.Platform.Web` | React + Vite + Tailwind/Radix (shadcn) | Platform admin **and** club-owner console (browser) | Modular: `i18n/`, `club/money.ts`, `components/`, `api/` (verified) |
| `AFK4.Operator.App` | .NET 10 WPF desktop | Floor-operator console (native host) | `MainWindow.xaml.cs` still wires legacy WPF FloorMap/Shell VMs; `SeatContextPanelViewModel.cs` = **686 lines** (verified) |
| `AFK4.Operator.App.Web` | React + Vite (raw CSS) | Floor-operator UI embedded in the WPF host via WebView2 + `hostBridge` | `App.tsx` = **9,828 lines** monolith (verified) |

**The duplication is real and drifting.** The WPF operator console and the React operator
console implement the *same* operator features — floor map, sessions, POS, players, shifts,
settings — twice, and they have already diverged:

- **Identity entry drift.** WPF `SeatContextPanelViewModel` exposes raw GUID text inputs —
  `PlayerAccountIdText`, `TariffVersionIdText`, `PlayerPackageIdText`, `TargetSeatIdText`
  (verified, lines 43–50), parsed via `TryParseOptionalGuid`. The React operator UI uses
  backed player-search and tariff/package dropdowns against existing APIs. Same backend,
  two very different ergonomics — and the GUID path is an explicit non-negotiable *defect*
  per the migration plan.
- **Money drift.** WPF hard-codes `MoneyImpactText => $"0.00 {currencyCode}"` (verified,
  line 173) — no live cost at all. The React app rolls its *own* `formatMinorUnits` /
  `formatMoneyInputMinorUnits` that divide minor units by **100** inline (verified, lines
  1152, 1192). Meanwhile `Platform.Web/src/club/money.ts` already has tested
  `minorToMajor`/`majorToMinor` helpers (with an EPSILON-nudged round) that are the
  project's blessed convention. Three different money implementations across two web apps
  and one desktop app.
- **i18n drift.** `Platform.Web` has a **1,318-line** message catalog (`i18n/messages.ts`)
  behind an `I18nProvider`. The operator React app has **no catalog** — strings are
  hard-coded Russian inline with `Intl.NumberFormat('ru-RU', …)` sprinkled through
  `App.tsx` (verified). The localization spec's work would otherwise have to be done twice.
- **No shared code.** The two React apps are **separate npm packages** with no shared
  workspace: duplicated React/Vite/test toolchains and already-drifting deps
  (`lucide-react` 1.16 vs 1.17), different styling stacks (operator = raw CSS; platform =
  Tailwind + Radix/shadcn). Nothing is reused; everything is re-authored.

**The migration is already in flight and well advanced.** `App.xaml` has
`StartupUri="Web/WebViewOperatorWindow.xaml"` (verified) — the **WebView2 shell is already
the default startup window**. The legacy WPF `MainWindow` and its ViewModels remain in the
project *only as parity reference* per the migration plan's Task 8. The host bridge today
exposes a deliberately narrow surface — `auth:*` and `connection:*` only (verified in
`OperatorWebHostBridge.cs`); native protected-token storage lives host-side.

So the strategic questions are no longer "should we migrate" but: **commit to the React
operator UI as the single operator surface, finish retiring the WPF view-logic, decompose
the 9.8k-line `App.tsx`, and stop the web↔web drift with a shared library** — and decide
whether to do this **before** the heavy operator-UX investment in the counter-loop /
realtime / localization specs.

## 2. Goals

1. **One operator surface.** Make the React `Operator.App.Web` the *single* operator UI;
   reduce `AFK4.Operator.App` to a **thin WebView2 host** owning only native concerns.
2. **Retire duplicated WPF view-logic** once parity is proven, removing the drift source.
3. **Define the native/web boundary** explicitly and durably (what may *never* move to web).
4. **Shared web library strategy** with `Platform.Web`: one design-system, one i18n catalog,
   one money util, shared API-client/DTO types — so counter-loop / realtime / localization
   ergonomics are authored once.
5. **Decompose the 9,828-line `App.tsx`** into a maintainable module/feature structure
   without changing the accepted visual baseline.
6. **Verify parity with no feature regressions** at every cutover step.

### Non-goals (explicitly out of scope / deferred)

- **No new operator features.** Counter-loop open-tab/checkout, realtime consistency, and
  localization land in *their* specs; this spec only makes them cheap to build once.
- **No browser-delivered operator panel.** The operator UI stays a native WebView2-hosted
  desktop app per the migration plan's non-negotiables; we are not exposing it as a web URL.
- **No backend/contract changes.** Backend stays the single authority for sessions, money,
  POS, shifts, devices; DTO intent is the contract baseline.
- **No change to `Player.Shell` / `Agent.Service`.** Kiosk/lock/shell-replacement on the
  *gaming PC* is a separate native concern and surface; untouched here.
- **No full design-system rewrite of `Platform.Web`.** We extract from it, we don't redo it.
- **Not a monorepo tooling migration** (Nx/Turbo) beyond what a workspace needs.

## 3. Proposed Decisions

These are the forks the founder should ratify. Each is stated as a recommendation with the
alternative and rationale.

| # | Decision | **Proposed** | Alternative(s) | Rationale |
|---|----------|--------------|----------------|-----------|
| D1 | **Canonical operator UI** | **React `Operator.App.Web` in a WebView2 host** is the single operator surface; WPF view-logic retired | Keep WPF as primary; keep both | Migration already defaults to the WebView2 window; React UI is further along (search/dropdowns, dashboard, booking, POS, settings wired). Two surfaces = permanent drift tax. |
| D2 | **Code-sharing approach** | **Shared internal package(s) in a bun/npm workspace** consumed by both web apps | Continue duplication; or copy-paste "shared" files | Money/i18n/DTO drift is *already* causing bugs (÷100 vs `minorToMajor`, no catalog). A versioned in-repo package makes the blessed util the path of least resistance. Full monorepo tooling is overkill; a workspace is enough. |
| D3 | **`App.tsx` decomposition** | **Decompose into feature modules** (`features/*`) + `shared/` *before* heavy feature work, behind the accepted visual baseline | Defer; keep editing the monolith | 9.8k lines in one file makes counter-loop/realtime edits high-risk and unreviewable. Decompose first so later specs touch small modules. |
| D4 | **Sequencing vs operator-UX investment** | **Consolidation PRECEDES heavy operator-UX work** (counter-loop checkout, realtime, localization) | Build features now, consolidate later | Building ergonomics on a monolith with drifting utils means building them *twice* (WPF + web) or rebuilding them post-extraction. Pay the structural cost once, first. |
| D5 | **WPF retirement timing** | **Retire WPF view-logic per-workspace, gated on a parity checklist**, not big-bang | Delete now; or keep indefinitely | Plan Task 8 already gates removal on verified pilot-day parity. Per-workspace retirement keeps a rollback story. |
| D6 | **Styling convergence** | **Operator web adopts `Platform.Web`'s Tailwind + shared tokens** for *new/decomposed* modules; existing accepted screens migrate opportunistically | Keep operator raw-CSS; or rewrite all screens now | A shared design-system needs one styling substrate. Don't rewrite the accepted baseline in a flag day; converge as modules are extracted. |
| D7 | **Shared package boundary** | Share **pure/headless** code (money, i18n catalog+provider, API client + DTO types, formatting, validation); **do not** force-share full visual components v1 | Share everything incl. components; share nothing | Headless utils are low-risk and high-drift-payoff. Visual components differ (dense operator grid vs owner console) — share tokens/primitives, not whole screens, initially. |

## 4. Architecture Overview

Target topology: **one native thin host per device-role, one React surface per audience, one
shared library underneath.**

```
                         ┌──────────────────────────────────────────────┐
                         │            packages/ (bun workspace)          │
                         │  @afk4/money   @afk4/i18n   @afk4/api-client   │
                         │  @afk4/ui-tokens   @afk4/formatting           │
                         └───────────────┬───────────────┬──────────────┘
                                 imports │               │ imports
                ┌────────────────────────┘               └───────────────────────┐
                ▼                                                                  ▼
   ┌─────────────────────────────┐                              ┌─────────────────────────────┐
   │  Operator.App.Web (React)   │                              │   Platform.Web (React)      │
   │  features/{map,pos,clients, │                              │   admin + club-owner console │
   │   payments,booking,logs,    │                              │   (browser-delivered)        │
   │   settings,dashboard}       │                              └─────────────────────────────┘
   │  + shared/ (app shell)      │
   └──────────────┬──────────────┘
   served as local│ assets + window.chrome.webview
                  ▼
   ┌─────────────────────────────────────────────┐         ┌──────────────────────────────────┐
   │  AFK4.Operator.App  (THIN .NET host)         │         │   AFK4.Platform.Api  (authority) │
   │  WebView2 window lifecycle                   │◀────────│   sessions · money · POS · shifts │
   │  protected token + connection storage        │  HTTPS  │   devices · audit · realtime hub  │
   │  hostBridge (auth:*, connection:*, +native)  │────────▶│                                   │
   │  hotkeys · single-instance · auto-launch     │         └──────────────────────────────────┘
   │  OS/window kiosk for the COUNTER station     │
   └─────────────────────────────────────────────┘
                  ( legacy WPF FloorMap/Sessions/Pos/... VMs: DELETED at cutover )

   ┌──────────────────────────────────────┐
   │ AFK4.Player.Shell / Agent.Service     │  ← gaming-PC kiosk/lock/shell-replacement.
   │ (gaming-PC native; OUT OF SCOPE here) │     A SEPARATE native surface — not consolidated.
   └──────────────────────────────────────┘
```

Two native surfaces remain, by design and for different reasons:
- the **operator host** (`Operator.App`) — a thin chrome around the React operator UI;
- the **gaming-PC shell/agent** (`Player.Shell`/`Agent.Service`) — true kiosk/lock/OS
  enforcement on the customer PC, never a web surface.

`Platform.Web` stays browser-delivered (no host) and is the *donor* of the shared library.

## 5. Components

### 5.1 Thin operator host (`AFK4.Operator.App`)

**What stays native (the durable boundary):**

- WebView2 window lifecycle, single-instance, app config, staging env vars
  (`AFK4_OPERATOR_*`) — already present.
- **Protected token & connection storage** (`ProtectedDataOperatorTokenStore`,
  `ProtectedDataOperatorConnectionStore`) — must stay host-side; the non-negotiable is that
  frontend tokens are never persisted in browser `localStorage`.
- **Host bridge** (`OperatorWebHostBridge`) — today `auth:*` + `connection:*`. May grow
  *narrow* native verbs (printer/receipt, app diagnostics, auto-update trigger, OS
  power/lock of the **counter** station) — each added deliberately, schema'd, and tested.
- **Auto-launch / shell behaviour** of the *counter* machine, hotkeys
  (`OperatorHotkeyService`), MSI/update packaging (component name `operator-app`),
  WebView2-runtime prerequisite handling.

**What leaves native (deleted at cutover):** every `*WorkspaceViewModel`,
`SeatContextPanelViewModel`, `FloorMapSeatViewModel`, the WPF `Http*ApiClient` per-feature
clients, and the legacy `MainWindow` visual tree. These are pure duplication of React +
typed API clients.

**Host bridge contract (keep narrow):** continue the `verb:noun` JSON message shape already
in `OperatorWebHostBridge.HandleAsync`. New verbs require: a typed payload, a host test, and
a graceful "bridge unavailable" browser-dev fallback (already a typed diagnostic).

### 5.2 Shared web library (bun workspace)

Introduce a `packages/` workspace (bun, matching the frontends-on-bun-test convention) with
small, versioned, **headless** packages. v1 set:

| Package | Source today | Consumers | Notes |
|---|---|---|---|
| `@afk4/money` | extract `Platform.Web/src/club/money.ts` (`minorToMajor`/`majorToMinor`, currency-aware format) | both web apps | **Deletes** operator's inline `÷100` `formatMinorUnits`/`formatMoneyInputMinorUnits`. Single rounding convention. |
| `@afk4/i18n` | extract `Platform.Web/src/i18n` (`I18nProvider`, `messages.ts`) | both web apps | Operator web gains a catalog instead of inline RU strings; localization spec then edits one catalog. |
| `@afk4/api-client` + DTO types | unify `Platform.Web/src/api/*` and operator `operatorApiClients.ts`/`platformApi.ts` typing | both web apps | One typed client boundary + shared DTO mirror of `AFK4.Shared.Contracts`. Reduces hand-rolled `readMoney`/`readString` parsers in operator `App.tsx`. |
| `@afk4/ui-tokens` | `Platform.Web` Tailwind theme/tokens | both web apps | Shared color/spacing/status tokens (the documented operator status tones). Components stay per-app v1 (D7). |
| `@afk4/formatting` | date/number `Intl` helpers (currently re-declared per app) | both web apps | One `ru-RU` (and future-locale) formatting source. |

**Guardrail:** shared packages are **framework-light and side-effect-free** (no app state,
no router). Each ships its own `bun test` suite (the money/i18n suites already exist and
move with the code). Apps depend on workspace versions, not relative `../../` reaches.

### 5.3 `App.tsx` decomposition (the 9,828-line monolith)

Decompose **behind the accepted visual baseline** — no redesign — into a feature-module tree
mirroring the migration plan's eight workspaces:

```
src/AFK4.Operator.App.Web/src/
  app/            shell, routing, permission-aware navigation, providers
  features/
    map/          floor map + selected-seat panel + session actions
    dashboard/    summary KPIs, focus queue, exports
    booking/      reservation search/create/seat/cancel
    pos/          cart, checkout, refund/void, receipts, customer lookup
    clients/      player search, wallet/debt, packages
    payments/     shift open/close, cash movements, reports/CSV
    logs/         audit/diagnostics filters, exports
    settings/     profile, personnel, layout/devices, tariffs, integrations
  shared/         hooks, error projection (apiErrors.ts), realtime state
                  (operatorRealtime.ts), hostBridge.ts  (already separate)
```

**Method (mechanical, low-risk):** extract one feature at a time out of `App.tsx` into a
`features/<x>/` folder, keeping props/state wiring identical; assert the existing
`App.test.tsx` (and per-feature ports) stay green after each extraction. Money/i18n/format
references in each extracted module switch to the shared packages as it moves — so
decomposition and de-duplication happen in the *same* commit per feature, not as two passes.

### 5.4 `Platform.Web` (donor, mostly unchanged)

`Platform.Web` is already the well-factored app and the source of the shared utils. Its only
change in this spec: its in-app `club/money.ts`, `i18n/`, theme tokens, and api typing become
re-exports of the workspace packages (so there is exactly one implementation). No visual or
behavioural change; existing tests must stay green to prove it.

## 6. Code-Move / Ownership Table

| Concern | Today | Target | Action |
|---|---|---|---|
| Operator floor map / seat panel | WPF `FloorMap/*VM`, `SeatContextPanelViewModel` **and** React `App.tsx` | React `features/map/` only | Retire WPF (D5); extract React module (§5.3) |
| Operator POS/Clients/Payments/Booking/Logs/Settings | WPF `*WorkspaceViewModel` **and** React `App.tsx` | React `features/*` only | Retire WPF; extract React modules |
| Operator per-feature HTTP clients | WPF `Http*ApiClient` **and** React `operatorApiClients.ts` | `@afk4/api-client` (typed) | Retire WPF clients; unify React client |
| Money minor↔major | WPF `MoneyImpactText` hardcode; React inline `÷100`; `Platform.Web/club/money.ts` | `@afk4/money` | One util; delete the other two |
| i18n / strings | `Platform.Web/i18n` catalog; operator inline RU | `@afk4/i18n` | Operator gains catalog; one source |
| Date/number formatting | re-declared `Intl` per app | `@afk4/formatting` | One source |
| Design tokens / status tones | `Platform.Web` Tailwind; operator raw CSS | `@afk4/ui-tokens` | Shared tokens; components per-app v1 |
| Native token/connection storage | WPF host (`ProtectedData*Store`) | **unchanged** (host) | Stays native |
| Host bridge | `OperatorWebHostBridge` (`auth:*`,`connection:*`) | **unchanged + narrow growth** | Native verbs only |
| Window/hotkeys/auto-launch/MSI | WPF host | **unchanged** (host) | Stays native |
| Gaming-PC kiosk/lock/shell | `Player.Shell` / `Agent.Service` | **unchanged, out of scope** | Separate surface |

No backend/contract entity changes; this is a frontend topology + code-ownership move.

## 7. Risks, Error Handling & Boundary Rules

- **Parity regression on WPF retirement.** *Mitigation:* per-workspace parity checklist
  (§8) gates each deletion; legacy WPF stays in git history for rollback; nothing is deleted
  before its React workspace passes staging smoke (already the plan's Task 8 stance).
- **Decomposition behaviour drift.** *Mitigation:* extraction is mechanical (move, don't
  rewrite); the existing `App.test.tsx` + per-feature suites must stay green at each step;
  no visual changes in the same commit as a de-dup change is forbidden from also redesigning.
- **Money double-conversion.** The known hazard (MEMORY: `formatCurrency` takes *major*
  units; convert at the UI boundary). *Mitigation:* `@afk4/money` is the only converter;
  lint/forbid raw `/ 100` and `* 100` on money in both web apps once migrated.
- **Shared-package coupling / version churn.** *Mitigation:* packages are headless and
  side-effect-free (D7); breaking changes are caught by both apps' test suites in one CI run
  since they share the workspace.
- **Host-bridge scope creep.** Risk that web logic leaks into native verbs. *Mitigation:*
  bridge verbs are limited to things that *require* OS/native access (storage, printer,
  power, auto-update); each new verb needs a host test and a browser-dev fallback.
- **Token-storage invariant.** Non-negotiable: tokens never in browser `localStorage`. The
  shared `@afk4/api-client` must obtain tokens via the host bridge (operator) or the
  existing browser-auth path (platform) — it must not introduce its own persistence.
- **Styling flag-day risk.** *Mitigation:* D6 — operator screens converge to Tailwind/tokens
  opportunistically as modules extract, never in one big rewrite.

## 8. Parity Verification & Migration Strategy

**Per-workspace parity checklist** (must pass before retiring the matching WPF VM):

1. Every action the WPF VM exposes has a React equivalent wired to the *same* backend
   contract, in pending/confirmed/failed states (no fixture-success).
2. No raw GUID/form-first path in the normal operator flow (search/dropdown parity) — the
   migration plan's explicit non-negotiable.
3. Money shown via `@afk4/money` (no hardcoded `0.00`, no inline `÷100`).
4. Strings via `@afk4/i18n` catalog (no new inline RU literals in the extracted module).
5. Staging smoke against `https://afk4.staging.mubi.dev` records sign-in → action →
   backend-confirmed result evidence (the plan's Task 8 gate).

**Verification gates (CI + manual):**

- `bun test` green for each shared package and both web apps after every extraction.
- `dotnet test AFK4.sln` green after each WPF deletion (host tests, packaging-invariant /
  MSI-content assertions intact — frontend assets still shipped in `WebAssets`).
- Operator App builds and opens to the React UI; MSI still includes host binaries + built
  frontend assets + WebView2 prerequisite handling.
- A short **side-by-side parity log** per workspace (WPF action vs React action vs backend
  result) attached to the cutover PR.

**Cutover is reversible** until the final WPF deletion: the legacy `MainWindow` window can be
re-pointed as `StartupUri` in an emergency while it still exists in-tree.

## 9. Decomposition & Sequencing

Recommended order (D4: this whole spec precedes heavy counter-loop/realtime/localization UX):

1. **Stand up the bun workspace + `@afk4/money` + `@afk4/i18n`.** Re-point `Platform.Web` to
   them (prove with its green tests). Lowest-risk, highest-drift-payoff first.
2. **Add `@afk4/formatting` + `@afk4/api-client`/DTO types**; introduce a thin operator
   `app/` + `shared/` scaffold (shell, providers) without moving features yet.
3. **Decompose `App.tsx` feature-by-feature** into `features/*`, switching each module's
   money/i18n/format/client references to the shared packages in the same commit (§5.3).
   Order by churn/criticality: `map` → `pos` → `clients`/`payments` → `booking`/`logs`/
   `settings`/`dashboard`.
4. **Retire WPF view-logic per workspace** as each React feature passes the §8 checklist on
   staging (D5). Keep `MainWindow` until the last workspace is done, then delete it and the
   per-feature WPF clients; host shrinks to lifecycle + storage + bridge + packaging.
5. **`@afk4/ui-tokens` + opportunistic Tailwind convergence** of operator modules (D6) —
   runs alongside 3–4, never as a flag day.
6. **Lint guardrails** (forbid raw money `*100`/`/100`, forbid new inline UI strings) once
   both apps consume the shared packages.

Only after 1–4 land do the **counter-loop checkout**, **realtime-consistency**, and
**localization** specs build their operator ergonomics — once, in `features/*`, on shared
utils.

## 10. Future (v2 / follow-on)

- **Shared visual component layer** (`@afk4/ui`): promote stable primitives once tokens have
  settled and the operator/owner component needs converge (deliberately deferred past D7 v1).
- **Shared SignalR/realtime client** package once the realtime-consistency spec stabilises
  the event contracts (currently per-app `operatorRealtime.ts`).
- **Contract-generated DTO types** (codegen from `AFK4.Shared.Contracts`) to replace the
  hand-mirrored types in `@afk4/api-client`, killing the `readMoney`/`readString` runtime
  parsers in the operator app.
- **Counter-station kiosk/lock** native verbs in the host bridge if the counter machine
  itself needs lock-down (distinct from gaming-PC `Player.Shell`).
- Evaluate full monorepo tooling (Turbo/Nx) only if the workspace's build graph outgrows
  plain bun workspaces — not before.
