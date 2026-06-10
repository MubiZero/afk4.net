# Customer Shell WebView2 Pivot — Design

**Date:** 2026-06-09
**Status:** Approved design, pending implementation plan
**Supersedes:** the backlog item "customer-shell Units 4–5 (WPF self-service)"

## Goal

Re-architect the gaming-PC customer shell (`AFK4.Player.Shell`) from a pure
WPF/XAML full-screen kiosk into a **thin native host + React UI in WebView2**,
reusing the project's web design system. Target: smartshell.gg-class self-service
UX. Enforcement (lock/kiosk, process policy, lease, offline-lease) stays in
`AFK4.Agent.Service` and is not touched. The shell remains a **non-authoritative
"face"**: the server and Agent own billing, session rights, and policy.

## Scope

### In scope (MVP cut line, agreed)

- Login + game launcher (covers, recent, search)
- Session timer + warnings
- Self extend-time
- Self top-up / payment on the PC via dcgate (**QR / DC-pay flow**, not a card form)
- Tariffs / packages (not raw minutes)
- Shop (snacks/drinks ordered to the seat)
- Loyalty / cashback content screens
- News / banners
- "Call operator"
- Language switch, pause / lock-without-ending

### Deferred to Phase 2 (must not forget)

- **Game account vault** — store Steam/Epic/Riot/Battle.net logins, shell
  auto-logs the player in. Signature smartshell feature; heavy, security-sensitive,
  lives mostly on the Agent.Service side.
- **Privacy wipe on logout** — clear logins/saves/cookies between players
  (Agent.Service side; UX button in shell).
- Tournaments/brackets in shell, player-to-player chat, achievements/badges,
  player-side theme customization.

## Approach

Chosen: **dedicated `Player.Shell.Web` React app on the shared design system +
thin native host** (vs. single-app-with-kiosk-flag, vs. WPF-with-embedded-WebView2
panels).

Rationale: cleanly separates two very different contexts (phone PWA vs. gaming-PC
kiosk); offline and fallback are first-class; kiosk specifics (launcher, native
bridge, lease) do not pollute `Customer.Web`; release cycles stay decoupled; and it
mirrors the already-proven `Operator.App` pattern (thin native host + React in
WebView2) and the established monorepo pattern of one web app per surface sharing
`@afk4/*` packages.

This design is validated against industry best practice:

- **Thin native host + system WebView2** is the Tauri pattern (system WebView, thin
  native core) — the modern, production-ready alternative to bundling Chromium.
- **Privileged service separate from interactive UI** is required by Windows
  **Session 0 isolation**: services run elevated in Session 0, UI in the user
  session, communicating via named pipes — exactly the Agent.Service ↔ Player.Shell
  split.
- **Untrusted UI, server-authoritative** is the standard kiosk-lockdown model.
- **Narrow JS↔native bridge** is the #1 WebView security best practice (the bridge
  is the most dangerous attack surface).
- **Local content via virtual-host mapping** is Microsoft's recommended way to load
  a bundled web build (and yields a real HTTP origin so `localStorage`/`IndexedDB`
  and origin validation work).

## Architecture and native/web boundary

```
☁️ AFK4.Platform.Api (source of truth)
   REST: login, sessions, tariffs, top-up (dcgate), shop, loyalty, news
   SignalR DeviceHub: commands, lifecycle events, remaining time (realtime epic)
        ▲ SignalR (Agent holds the connection)   ▲ REST (web calls, native injects token)
┌─────────────────────────── GAMING PC ───────────────────────────┐
│ 🛡️ Agent.Service              │ 🖥️ Player.Shell — thin native host │
│ NATIVE · Win Service · elevated│ NATIVE (.NET, like Operator.App)   │
│ AUTHORITY                      │ - full-screen window = LOCK BOUNDARY│
│ - lock/unlock, kiosk policy    │ - native fallback panel + watchdog  │
│ - allow/deny processes         │ - JS↔native bridge (narrow, secured)│
│ - LEASE (offline right to play)│ - named pipe → Agent.Service (exists)│
│ - SignalR to cloud, heartbeat  │ - launch games (LauncherClient, exists)│
│ - restore state after reboot   │   ┌───────────────────────────────┐ │
│                                │   │ WEB · WebView2 · Player.Shell.Web│ │
│                                │   │ React 19 + Tailwind 4 + Radix    │ │
│                                │   │ screens: login·timer·launcher·   │ │
│                                │   │ extend·top-up·shop·loyalty·news  │ │
│                                │   │ bundled locally (virtual-host) → │ │
│                                │   │ renders offline; @afk4/* shared  │ │
│                                │   └───────────────────────────────┘ │
│        Player.Shell ⇄ Agent.Service: named pipe (state, lease, cmds)  │
└──────────────────────────────────────────────────────────────────────┘
```

**Boundary principle:** the web renders and sends requests but never decides. The
lock, hardware, lease, process launch, and watchdog are native; billing, rights,
and policy are cloud+Agent. If the web dies, native holds the screen.

The named pipe between host and Agent is **ACL-restricted** (logon-SID DACL, `Local\`
namespace) so only a process in the same interactive session can use the channel
(named pipes are a known privilege-escalation vector otherwise).

## Components / units

### Native host (`Player.Shell`, rewritten; .NET)

- **`ShellHostWindow`** — full-screen borderless window, hosts WebView2, owns the
  lock boundary, toggles web ↔ native fallback.
- **`WebViewBootstrapper`** — ensures the WebView2 runtime, creates `CoreWebView2`,
  maps local assets via virtual host (`app://shell/` → build folder), disables
  devtools and the context menu in production, applies kiosk hardening.
- **`ShellBridge`** — the JS↔native contract (host-object + `postMessage`).
  Out to JS: session state, lease, remaining time, `launch(gameId)`,
  `requestOperator()`, `pause()`. In: state push. **Origin allowlist (only
  `app://shell`) + validation of every parameter from JS.** Deliberately narrow.
- **`WebViewWatchdog`** — listens for `ProcessFailed`/unresponsive; on crash shows
  the native fallback, restarts WebView2, reloads the UI.
- **`NativeFallbackView`** — minimal native render (timer + "recovering"), shown by
  the host when the web layer is unavailable.
- **Reused as-is:** `NamedPipePlayerShellStateClient` (state from Agent),
  `LauncherCommandClient` (launch games). The bridge sits on top of these.

### Web (`Player.Shell.Web`, new React app)

- Stack: Vite + React 19 + Tailwind 4 + Radix + cva; shared `@afk4/i18n`,
  `@afk4/money`, `@afk4/formatting`; design tokens shared with the other web apps.
- **`useShellBridge`** — hook wrapping the native bridge; exposes reactive session
  state + actions.
- **`useShellApi`** — REST client to Platform.Api with offline detection → degrade.
- **Screens (one module each):** Login · Active session (timer + launcher) · Extend ·
  Top-up/pay (QR) · Shop · Loyalty · News · Call operator.
- **Local cache** of launcher catalog and tariffs (IndexedDB, available thanks to
  the virtual-host origin) for offline rendering.

### Shared design system (YAGNI)

Do **not** create an `@afk4/ui` package yet. Share as the project already does:
common Tailwind preset + cva + Radix, base components copied shadcn-style (as in the
other four web apps). Extract `@afk4/ui` only if duplication becomes painful.

## Data flow

Three channels, each with one role:

1. **Pipe (host ⇄ Agent.Service) — works offline.** Authoritative *local* state:
   lock/unlock, remaining time (lease), session id, warnings, launch-command
   results. Native pushes it to the web via the bridge; React renders timer/launcher.
   `Agent → pipe → ShellBridge → useShellBridge() → UI`

2. **REST (web → Platform.Api) — online, degrades offline.** Self-service and
   content: login, tariffs, extend, top-up (dcgate), shop, loyalty, news. The web
   does `fetch()`; the **native host injects the auth token** by intercepting
   `WebResourceRequested` for the API origin and adding `Authorization`. The token
   never lives in JS/localStorage, so XSS cannot exfiltrate it.
   `UI → fetch(api) → [native injects Bearer] → Platform.Api`

3. **SignalR (Agent.Service ⇄ cloud) — one connection, not from the web.** The web
   does **not** open its own SignalR. The Agent holds the connection (heartbeat,
   commands, lifecycle — realtime epic). Realtime relevant to the shell ("time
   added", "session ended") arrives: `Cloud → SignalR → Agent → pipe → bridge → UI`
   (reconcile). One channel to the cloud, consistent with the realtime epic; the
   shell depends on the Agent as relay.

### Offline behavior (cloud unreachable)

- **Works:** timer (lease via pipe), launching allowed games, local cache of
  catalog/tariffs (IndexedDB).
- **Degrades:** top-up, new login, shop → "temporarily unavailable, call operator".
- Offline signal: pipe reports `cloud-down` OR `fetch` fails → `useShellApi` switches
  the affected screens to the degraded state.

## Error handling and resilience

Cross-cutting principle: **fail-locked / default-deny** — any uncertainty keeps the
screen locked, never exposes the desktop, never grants free time.

1. **WebView2 crash/hang** → `WebViewWatchdog` catches it → `ShellHostWindow` shows
   `NativeFallbackView` (native timer + "recovering"), restarts WebView2, reloads.
   The native window keeps covering the desktop; Agent.Service enforces independently.
2. **JS error in the web** → React error boundary renders an in-web fallback with
   "reload"; a fully blank/hung page is caught by the watchdog as unresponsive.
3. **Pipe disconnect (Agent restarted)** → the bridge shows "connecting" and retries;
   the window **stays locked** (no state ⇒ assume locked). Agent restores state on
   restart (already its responsibility).
4. **Cloud offline** → degrade (see Data flow).
5. **Version conflict (409) on extend/top-up** → reuse the realtime-epic pattern:
   the web shows "state changed, refreshing" and reloads authoritative state.
6. **Payment not settled (dcgate)** → the web shows an error; no time added (the
   server is authoritative; the web never "draws in" a result).

Genuinely new code here is only `WebViewWatchdog` + `NativeFallbackView` + bridge
reconnection; 409/lease/state-restore already exist or come from the realtime epic.

### Payment status principle (authority = webhook)

Grounded in the existing `PaymentIntentEntity` (intent + webhook confirmation):

A payment counts as settled **iff** the server moves the intent to `fulfilled`,
which happens **only** on a verified dcgate webhook (an incoming bank payment is
matched). The shell never decides and never "draws in" balance.

Intent states the shell reflects:

- **`pending`** — intent created; show QR + amount + countdown to
  `GatewayExpiresAtUtc` + "awaiting payment confirmation". Balance untouched.
- **`fulfilled`** — webhook confirmed and matched → balance credited server-side
  (double-credit guarded by **idempotency on the intent id**). Shell shows success
  and re-reads the balance from the server.
- **`expired`** — `GatewayExpiresAtUtc` passed without a match → "expired, start over".
- **`disputed`** (flag, stays `pending`) — money **not** credited; operator resolves
  → shell shows "payment under review, call operator".

"Not settled" = anything that is not `fulfilled` (expired, cancelled, stuck pending
past timeout, or disputed). Shell rule: **credit/grant nothing until `fulfilled`
arrives from the server**; "QR scanned" or "looks paid" is not success.

dcgate flow is **QR / DC-pay**: the shell renders a QR (`pay.dc.tj/...`), the customer
pays from their banking app, dcgate matches the incoming bank message against the
18-char comment and fires a webhook. No card form on the club PC.

## Work units (re-spec of customer-shell Units 4–5)

Each unit is an independently testable PR. Order is the build sequence.

- **Unit A — Native thin host + WebView2 + fallback/watchdog.** Rewrite
  `Player.Shell`: `ShellHostWindow`, `WebViewBootstrapper` (virtual-host mapping,
  kiosk hardening), `NativeFallbackView`, `WebViewWatchdog`. Loads a placeholder web
  build; keeps existing pipe/launcher clients. *Done when:* the native host renders a
  local web build full-screen and survives a WebView2 crash via the native fallback.
- **Unit B — Bridge contract (`ShellBridge` ↔ `useShellBridge`).** Narrow, secured
  bridge: state push native→web; actions (launch, requestOperator, pause) web→native;
  origin allowlist + input validation. Replaces the current WPF ViewModel binding.
  *Done when:* the web shows a live timer and launches a game through the bridge.
- **Unit C — `Player.Shell.Web` scaffold + active-session screen.** New React app,
  app shell, routing, `useShellBridge`, active-session screen (timer, launcher grid
  with covers/recent/search), warnings, language switch, "call operator".
- **Unit D — Login / session entry.** Login screen (account / session code / guest)
  on the existing player-auth REST. Token stored native (host injects). lock→unlock.
- **Unit E — Self-service online: extend + top-up (QR) + tariffs.** `useShellApi`
  (REST + degrade), extend screen, tariffs/packages screen, top-up screen (QR +
  intent status machine pending/fulfilled/expired/disputed) on existing dcgate
  endpoints; 409-reconcile from the realtime epic.
- **Unit F — Content screens: shop + loyalty + news.** Shop (catalog → order to
  seat), loyalty/cashback, news/banners; mostly server data + cards; offline →
  degrade.
- **Unit G — Packaging + offline cache + retire WPF UI.** Bundle the web build into
  the host installer, virtual-host wiring, IndexedDB cache of catalog/tariffs,
  WebView2 runtime prerequisite handling. Remove the old WPF `MainWindow`/ViewModel
  render (keep pipe/launcher infra). Update client packaging.

Dependencies: **A→B→C** is the foundation; then **D**; then **E** and **F** (can run
in parallel); **G** last (or packaging pulled in incrementally).

## Testing

Project patterns: TDD; `bun test` for web, xUnit for .NET. Key technique: keep
WebView2-touching code thin and put logic in testable policies (as already done for
`Operator.App`, whose host logic is tested independently of WebView2).

### Native host (.NET, xUnit)

- **Bridge contract** — state-DTO serialization, action dispatch, origin validation,
  input validation (bridge logic separated from WebView2 → pure unit tests).
- **Watchdog policy** — `ProcessFailed`/unresponsive → decision "fallback + restart"
  as a testable policy object (like `HeartbeatIntervalPolicy`).
- **Fail-locked** — no pipe state / no lease → locked (pure function).
- WebView2 itself is not unit-testable headless on Linux; keep WebView2 glue thin.

### Web (`Player.Shell.Web`, bun test + Testing Library)

- **`useShellBridge`** — mock the native bridge (`window.chrome.webview`); assert
  state reactivity and action calls.
- **`useShellApi`** — mock `fetch`; online success + offline degrade switching.
- **Screens** — render tests: session (timer, launcher), login, top-up (QR + status
  machine pending→fulfilled→expired/disputed), degrade states.
- **Payment status machine** — unit test the intent reducer separately.

### Contract / integration

- **Single source of truth for the bridge shape** (TS types ↔ C# DTOs) — test that
  both sides serialize the same shape (reuse the `Shared.Contracts` approach already
  used to generate TS for the operator).
- **Payment** — reuse existing Platform.Api dcgate tests (intent→webhook→fulfilled,
  idempotency, disputed); add coverage for any new shell-facing endpoints.

### Manual / on-device (not automatable on Linux; run on Windows bridge + staging)

- WebView2 crash → native fallback.
- Kiosk full-screen escape attempts.
- Real QR payment e2e on staging.

TDD discipline: web units and .NET policies are test-first; the thin WebView2 glue is
verified on the Windows bridge.
