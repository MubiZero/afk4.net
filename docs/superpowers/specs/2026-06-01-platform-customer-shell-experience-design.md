# Customer Experience on the Gaming PC — Shell Self-Service (Track 3)

- **Date:** 2026-06-01
- **Status:** Design (proposed decisions flagged for founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], full-product UX audit (2026-06-01),
  counter-loop (2026-06-01-platform-counter-loop-postpaid-checkout-design),
  customer-portal (2026-06-01-platform-customer-portal-design),
  notifications-backbone (2026-06-01-platform-notifications-backbone-design),
  localization (2026-06-01-platform-localization-design)

## 1. Context & Problem

The customer-facing surface on the gaming PC is **AFK4.Player.Shell** — a WPF fullscreen
lock screen driven by **AFK4.Agent.Service** over named-pipe IPC. It is today the weakest
surface in the product (~20% of best practice). Compared with ggLeap/ggClient and Gizmo,
where a member sits down, logs in at the PC, sees their balance and time, self-starts, and
tops up without ever talking to staff, our shell does almost none of that.

Verified ground truth:

- **It is a passive lock screen.** `MainWindow.xaml` renders a header with `RemainingTimeText`,
  a centred `StatusMessage`, a single warning `Border`, and a 4-column launcher grid bound to
  `LauncherApps`. `PlayerShellViewModel` exposes only `State`, `StatusMessage`,
  `RemainingTimeText`, `IsLocked/IsSessionActive/IsGraceMode`, `ShowWarning`, `ShowLauncher`,
  and `LaunchCommand`. **No member login, no balance/wallet, no debt, no self-start, no
  self-extend, no top-up.**
- **State is operator-driven and read-only.** The shell receives `PlayerShellStateDto`
  (`OrganizationId, BranchId, DeviceId, State, SessionId, LeaseExpiresAtUtc, RemainingSeconds,
  IsOnline, IsGraceMode, WarningThresholdSeconds, Message, LauncherApps`) over a one-way named
  pipe (`NamedPipePlayerShellStateClient` ← `NamedPipePlayerShellStateServer`). The agent
  builds this in `Worker.CreatePlayerShellState` with `WarningThresholdSeconds: 300` hardcoded.
  The shell's only outbound IPC is `launch-app` (`PlayerShellCommandHandler` rejects every other
  type). There is **no path from the shell back to the backend** today.
- **The warning text is hardcoded English** — `MainWindow.xaml` line 57:
  `"Session time or connectivity needs attention"` — in a Russian/Tajik market. The fallback
  `StatusMessage` strings in both `PlayerShellViewModel` and `Worker` are English too. This must
  be coordinated with the localization spec.
- **No public player auth exists.** Wallet endpoints are staff-protected:
  `GET /api/players/{id}/wallet-summary` requires `ViewBilling`;
  `POST /api/players/{id}/wallet/top-ups` requires `TopUpWallet`; player search
  `GET /api/branches/{branchId}/players` requires `ViewPlayers`. `WalletSummaryDto`
  (`PlayerAccountId, WalletBalance, DebtBalance, RecentEntries`) already models exactly what a
  member would want to see, but it is reachable only with a staff JWT. `PlayerAccountEntity` has
  **no credential at all** — just `DisplayName`, `PhoneNumber`, `IsActive`. There is no PIN, QR
  token, or player session anywhere.
- **Device commands are lock/unlock only.** `DeviceCommandTypeNames` = `lock`, `unlock`,
  `refresh-session-lease`. The backend is the single authority for session/lock state
  (Phase 8 rule): **the shell is presentation only and must never be trusted for billing or
  authorization.**

This design adds a member-facing self-service layer **on top of** that lock screen — login,
balance/time/debt, self-start, self-extend, in-shell top-up, branded theming, and a grace-mode
"top up to keep playing" flow — **without** moving any authority into the shell. Every action is
a request the backend validates; the shell only renders backend-confirmed state.

## 2. Goals

1. **Member login on the PC** (PIN, optional QR) returning a short-lived, **player-scoped**
   session token, distinct from staff/operator auth.
2. **On-screen balance + time remaining + debt**, with prominent low-time and low-balance
   warnings, localized.
3. **Self-start** for members with wallet credit, and **self-extend** for an active member
   session — both backend-validated.
4. **In-shell wallet top-up** (v1 without a live payment gateway — see D5).
5. **Branded/themed lock screen** plus a **grace-mode UX** that offers "top up to keep playing"
   before a hard lock, coordinated with the counter-loop auto-protection warnings that already
   push to the shell.

Throughout: shell calls backend, backend validates, no authoritative state in the shell.

### Non-goals (deferred or owned elsewhere)

- **Postpaid walk-in capture and unified checkout** — owned by the counter-loop spec. Strangers
  with no wallet still start via the operator counter (the hybrid model).
- **Mobile/web customer portal** (account history, remote top-up) — owned by the customer-portal
  spec; this spec shares the *player identity* it defines.
- **Notification/email backbone, transactional email, password/PIN reset over email/SMS** — owned
  by the notifications-backbone spec; this spec consumes its **player auth** primitives.
- **Full localization framework** (resource files, locale negotiation) — owned by the
  localization spec; here we only flag every shell string that must route through it and supply
  Russian as the launch default.
- **A real payment gateway integration** — explicitly out of scope and flagged as a hard
  dependency (D5).

## 3. Proposed Decisions (for founder review)

These are forks where I have baked in a best-practice default and flag the alternative.

| # | Decision | Proposed default | Alternative / note |
|---|----------|------------------|--------------------|
| **D1** | **Auth method at the PC** | **PIN + optional QR.** Member signs in with phone (or short member number) + a 4–6 digit PIN; QR is an optional fast path (member shows a code from the future portal/app, or the operator presents one). PIN is the floor because it needs no second device. | Alternatives: QR-only (needs a phone every visit — bad for a walk-up club PC), or operator-hands-off-session (no member identity). |
| **D2** | **Self-start eligibility** | **Members with wallet credit only.** A member who can cover the tariff's minimum-billable charge may self-start; everyone else (postpaid strangers) still starts via the operator counter per the hybrid model. | Alternative: allow postpaid self-start up to a credit limit — defer; the counter-loop credit-limit machinery would be the prerequisite. |
| **D3** | **Self-extend** | **Allowed for an active member session while covered by wallet (or within the postpaid credit limit the counter-loop spec defines).** Reuses `POST /api/sessions/{id}/extend`, just behind a player token instead of a staff token. | — |
| **D4** | **Top-up payment mechanism in v1** | **"Request top-up → operator confirms".** The member taps an amount; the backend raises a **pending top-up request** that surfaces on the operator floor map; the operator takes cash/card and confirms, which runs the existing `TopUpWallet` flow. Optionally show a **static QR-payment placeholder** (bank/e-wallet) the operator reconciles manually. **No balance moves until a staff actor confirms.** | This is the only mechanism that is safe **without** a gateway. Flag: a real gateway (card/e-wallet auto-capture) is a separate dependency before true unattended top-up. |
| **D5** | **Payment gateway** | **Not integrated today — hard dependency flagged.** v1 top-up is manual/operator-confirmed or QR-placeholder. The contracts are shaped so a gateway provider can later satisfy the same "pending top-up" request automatically. | Blocks fully unattended top-up; everything else in this spec ships without it. |
| **D6** | **Player session lifetime & idle** | **Short-lived player token** (e.g. 12h or session-bound), auto-signed-out on session end/lock and after an idle timeout, so the next walk-up member never inherits a previous member's identity. | — |
| **D7** | **Where player auth is defined** | **In the notifications-backbone/customer-portal shared identity**, not invented here. This spec consumes `POST /api/player-auth/...` and adds the PIN/QR credential fields to `PlayerAccountEntity`. If those specs land first, reuse as-is. | Avoids two divergent player-auth models. |
| **D8** | **Trust on lock** | **Backend remains the only authority.** Self-start/extend/top-up are *requests*; the shell shows optimistic "pending" UI but only unlocks/continues when the backend's `PlayerShellStateDto` says so. A compromised shell can never grant itself time. | Non-negotiable (Phase 8 rule). |

## 4. Architecture Overview

The shell gains an **outbound, player-authenticated channel to the backend**, alongside its
existing inbound state pipe from the agent. The agent stays the transport for lock/unlock/lease
and for backend-pushed warnings; the new player actions go **shell → Platform.Api directly**
(the PC already reaches the API for enrollment/heartbeat), carrying a **player token**, never a
staff token.

```
   Gaming PC                                         Platform.Api (authority)
 ┌─────────────────────────────┐
 │ AFK4.Player.Shell           │   player login (PIN/QR) ─────▶ PlayerAuthService
 │  - lock screen (themed)     │ ◀──────────── player token (scoped: this device/branch)
 │  - balance/time/debt        │   GET wallet-summary (player token) ─▶ WalletSummary
 │  - self-start / self-extend │   POST sessions/self-start ─▶ SessionSelfServiceService
 │  - top-up request           │   POST sessions/{id}/self-extend ──┐  (validates wallet/limit)
 │  - grace "top up to play"   │   POST players/{id}/top-up-requests │
 └───────────▲─────────────────┘                                    ▼
             │ named-pipe state (in)                 ┌─ start/extend → lease + unlock cmd
             │ launch-app (out)                      ├─ top-up request → pending (operator)
 ┌───────────┴─────────────────┐   lock/unlock/lease └─ auto-protection warn/lock (counter-loop)
 │ AFK4.Agent.Service          │ ◀──── device commands ───────  (existing device-command path)
 │  - builds PlayerShellStateDto│
 │  - enforces lease            │   operator confirms top-up ─▶ existing TopUpWallet flow
 └─────────────────────────────┘
```

Five independently testable components:

1. **Player auth at the PC** — PIN/QR credential on the player account + a player-scoped token +
   login/logout endpoints (shared with portal/notifications identity).
2. **Member status view** — balance/time/debt panel fed by a player-token `wallet-summary` plus
   the existing shell state, with localized low-time/low-balance warnings.
3. **Self-start & self-extend** — player-token session endpoints reusing the counter-loop's
   open-tab/lease/lock machinery; eligibility = wallet credit (D2/D3).
4. **In-shell top-up (v1 manual)** — a pending top-up request surfaced to the operator,
   confirmed via the existing `TopUpWallet`; gateway-ready contract (D4/D5).
5. **Theming & grace-mode UX** — branded lock screen + a "top up to keep playing" panel wired to
   the auto-protection warning the counter-loop already pushes (D8 trust boundary intact).

## 5. Components

### 5.1 Player auth at the PC

**Current state (verified):** `PlayerAccountEntity` has no credential. All player/wallet
endpoints require staff permissions. There is no player token type.

**Changes:**

- Add credential fields to `PlayerAccountEntity` (one EF migration):
  - `PinHash` (nullable, salted hash — never store the PIN), `PinSetAtUtc`,
    `PinFailedAttemptCount`, `PinLockedUntilUtc` (lockout after N failures).
  - `QrLoginTokenHash` (nullable) for the optional QR fast path — a rotating, single-device,
    short-lived token, not a permanent secret.
- New **player auth endpoints** (shared identity, per D7; defined here if the portal spec hasn't
  landed):
  - `POST /api/player-auth/pin` — body `{ branchId, deviceId, identifier (phone/member no.), pin }`
    → `{ playerToken, playerAccountId, displayName, expiresAtUtc }`. Rate-limited and lockout-aware.
  - `POST /api/player-auth/qr` — body `{ branchId, deviceId, qrToken }` → same response.
  - `POST /api/player-auth/sign-out` — invalidates the player token.
- **Player token** is a JWT (or opaque) carrying `playerAccountId`, `branchId`, `deviceId`, a
  `player` audience, and a short expiry (D6). It authorizes **only** the player-scoped endpoints
  below; it can never satisfy `Require*Permission` staff checks. PIN/QR set-up itself is an
  operator action (operator sets a member's initial PIN) until the customer portal can self-serve.

**Shell side:** a login view (phone/member field + PIN pad; optional "scan QR" affordance) shown
when `State == locked` and the device is member-self-service-enabled. On success the shell holds
the player token **in memory only** (never persisted) and switches to the member home view.

**Edge cases:** wrong PIN increments `PinFailedAttemptCount` and locks after N; an expired/lockout
token drops back to the staff-driven lock screen; the operator can always override via the counter.

### 5.2 Member status view (balance / time / debt)

**Reuse `WalletSummaryDto`** (`WalletBalance`, `DebtBalance`, `RecentEntries`) via a new
player-token-scoped read:

- `GET /api/player-self/wallet-summary` (player token) → the caller's own `WalletSummaryDto`. This
  is a thin, self-only wrapper over `LedgerBalanceProjector.GetWalletSummaryAsync`; it must reject
  any attempt to read another player's account (token's `playerAccountId` is the only allowed id).

**Display.** The member home view shows three primaries: **Balance**, **Time remaining**, **Debt
(if any)**. Time remaining continues to come from the shell's `PlayerShellStateDto.RemainingSeconds`
(authoritative lease), formatted by the existing `RemainingTimeFormatter`. Money is `long` minor
units end-to-end; convert minor→major only at the UI boundary (per the existing convention —
`minorToMajor` / `formatCurrency` on the web side; the WPF shell needs an equivalent boundary
formatter).

**Warnings.** Replace the single hardcoded English warning with **localized, typed** states:
low-time (`RemainingSeconds <= WarningThresholdSeconds`, already computed), low-balance
(balance below a configurable floor), and debt-present. Each is a localized message id (route
through the localization spec; **Russian is the launch default**, Tajik next). The warning
severity drives colour (amber low-time, red imminent-lock), respecting WCAG contrast.

**Transport.** Time/lease and lock state stay on the existing named pipe (authoritative). Balance
and debt come from the player-token API call, refreshed on login, after any top-up confirmation,
and on a low-frequency poll while active. The shell never computes authoritative balance.

### 5.3 Self-start & self-extend

**Self-start (D2).** New `POST /api/player-self/sessions/start` (player token) body
`{ seatId, tariffRuleVersionId, idempotencyKey }`:

- Resolves the player from the token (never from the body).
- Validates **eligibility**: the player's wallet must cover at least the tariff's
  minimum-billable charge (reuse the counter-loop `TariffBilling.ComputeAmount` minimum). If not,
  reject with a recoverable, localized "insufficient balance — top up" error that the shell turns
  into the top-up flow (§5.4).
- On success, reuses the **same** `SessionCommandService` start path as the operator (prepaid
  wallet mode), issues the lease, and dispatches **unlock** via the existing device-command path.
  The shell unlocks only when the backend-confirmed `PlayerShellStateDto` flips to `active`.

**Self-extend (D3).** New `POST /api/player-self/sessions/{sessionId}/self-extend` (player token)
→ thin wrapper over the existing `POST /api/sessions/{id}/extend`, but:

- Authorizes that the token's `playerAccountId` **owns** that active session.
- Validates wallet covers the extension (or postpaid credit limit, when the counter-loop limit
  machinery exists).
- Reuses the existing extend → lease-refresh → no-lock path.

**Trust (D8).** Both are *requests*; the shell may show an optimistic "starting…/extending…" state
but only transitions on the backend-pushed state. A forged player token can't pass the staff
authority checks, and the lease is signed by the backend as today.

### 5.4 In-shell top-up (v1 manual / gateway-ready)

**No live gateway today (D5).** v1 is **request → operator confirms**:

- New `POST /api/player-self/top-up-requests` (player token) body `{ amount, idempotencyKey }` →
  creates a **`TopUpRequestEntity`** (`TopUpRequestId, PlayerAccountId, BranchId, DeviceId,
  AmountMinorUnits, CurrencyCode, Status = pending, RequestedAtUtc, ConfirmedByStaffUserId?,
  ConfirmedAtUtc?, Method (cash|card|qr_manual)`). Status set: `pending → confirmed | rejected |
  expired`.
- The request surfaces on the **operator floor map / seat tile** as "Member requests +N TJS
  top-up". The operator collects cash/card and **confirms**, which runs the existing
  `POST /api/players/{id}/wallet/top-ups` (`TopUpWalletRequest`, `TopUpWallet` permission, audited)
  and marks the request `confirmed`. **No balance moves until a staff actor confirms** — this keeps
  the money path fully staff-authorized and auditable with zero gateway trust.
- Optional **static QR-payment placeholder**: show a branch-configured QR (bank/e-wallet) the
  member pays out-of-band; the operator reconciles and confirms the same request. Purely a display
  asset in v1.

**Shell side.** A top-up panel offers preset amounts; on submit it shows "waiting for operator to
confirm payment", then refreshes `wallet-summary` when the backend confirms (§5.2). In grace mode
(§5.5) the same flow is framed as "top up to keep playing".

**Gateway-ready (D5).** The `TopUpRequestEntity` + endpoint are shaped so a future payment-provider
webhook can move a request `pending → confirmed` automatically (provider as the "actor") without
changing the shell. That integration is a **flagged separate dependency**.

### 5.5 Theming & grace-mode UX

**Branding.** Replace the hardcoded palette/strings in `MainWindow.xaml` with a **branch theme**
(logo, primary/accent colours, club name) delivered as part of the shell config / state. Best
practice: a calm idle/lock screen (club brand, "tap to sign in"), a clear active HUD (time ↑,
balance, launcher), and high-contrast warning states. Apply `interface-limb`/accessibility
defaults: minimum 44×44 px touch targets for the PIN pad and launcher tiles, WCAG-AA contrast,
and motion that is subtle (no flashing on the low-time warning).

**Grace-mode "top up to keep playing".** The counter-loop auto-protection already pushes a
**warning** to the shell before a hard lock (fixed time-out at −5 min, or postpaid credit-limit
reached). This spec makes that warning **actionable** for members:

- When the pushed warning indicates "time/credit running out", the shell shows a prominent
  **"Top up to keep playing"** panel that deep-links into §5.4 (top-up request) and, on
  confirmation + sufficient balance, into §5.3 self-extend — so a member can avoid the lock
  without leaving the seat.
- If no top-up/extend happens, the backend's hard lock proceeds exactly as today (the shell never
  blocks its own lock — D8). The grace window itself stays backend-governed (the counter-loop /
  reliability specs own its duration).

**Warning channel.** Reuse the existing player-shell warning channel
(`PlayerShellStateDto.Message` + `WarningThresholdSeconds`, today hardcoded to 300 in
`Worker.CreatePlayerShellState`). To make warnings actionable and localized, extend the DTO with a
typed, structured warning (see §6) rather than a single English string, and make the threshold
configurable rather than a magic `300`.

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `PlayerAccountEntity` | add `PinHash`, `PinSetAtUtc`, `PinFailedAttemptCount`, `PinLockedUntilUtc`, `QrLoginTokenHash` (all nullable) |
| New `TopUpRequestEntity` | `TopUpRequestId, PlayerAccountId, BranchId, DeviceId, AmountMinorUnits, CurrencyCode, Status, Method, RequestedAtUtc, ConfirmedByStaffUserId?, ConfirmedAtUtc?` |
| New `PlayerToken` (auth) | player-scoped token type (`playerAccountId, branchId, deviceId, player` audience, short expiry); shared with portal/notifications identity |
| `PlayerShellStateDto` | add a typed, **localized** warning model (e.g. `WarningKind` enum: `none/low_time/low_balance/credit_limit/connectivity` + message id + severity) replacing the single hardcoded English string; make warning threshold configurable, not `300` literal |
| New endpoint | `POST /api/player-auth/pin`, `POST /api/player-auth/qr`, `POST /api/player-auth/sign-out` |
| New endpoint | `GET /api/player-self/wallet-summary` (self-only `WalletSummaryDto`) |
| New endpoint | `POST /api/player-self/sessions/start` (eligibility = wallet credit) |
| New endpoint | `POST /api/player-self/sessions/{id}/self-extend` |
| New endpoint | `POST /api/player-self/top-up-requests` (pending → operator confirms) |
| Operator surface | floor-map seat tile shows pending top-up requests + confirm action (runs existing `TopUpWallet`) |
| Shell strings | every `MainWindow.xaml` / `PlayerShellViewModel` / `Worker` English literal routed through localization (Russian launch default) |
| Branch config | shell theme (logo, colours, club name) + optional static top-up QR asset |

Each change carries an EF migration; money stays `long` minor units end-to-end, converted to major
units only at the UI boundary (existing convention). No authoritative state is added to the shell.

## 7. Error Handling & Edge Cases

- **Wrong PIN / lockout:** increment `PinFailedAttemptCount`; lock for a cooldown after N failures;
  surface a localized, non-enumerating error (don't reveal whether the identifier exists).
- **Stale/expired player token:** any player-self call returns 401 → shell drops to the lock/login
  screen; no silent privilege carry-over to the next walk-up member (D6).
- **Player token used on the wrong device/branch:** reject — the token is bound to `deviceId`/
  `branchId`; a leaked token can't roam to other PCs.
- **Self-start with insufficient balance:** recoverable, localized error that pivots into the
  top-up flow; never a half-started session.
- **Top-up requested but operator never confirms:** request **expires** (status `expired`) after a
  TTL; no balance moves; the member sees "request expired, ask staff".
- **Top-up confirmed but member already locked:** balance lands on the account; if a session is
  still resumable the member can self-extend, otherwise the credit persists for next time.
- **Double-tap / retries:** all player-self mutations take an `idempotencyKey` (existing pattern);
  retries collapse to the original result.
- **Backend unreachable from the shell:** player login/start/extend/top-up simply fail closed with
  a localized "can't reach server, ask staff"; the operator counter remains the fallback. (Offline
  buffering is the reliability spec's concern, not this one.)
- **Trust:** a tampered shell can fake UI but cannot mint a valid player token, cannot pass staff
  authority checks, and cannot unlock without a backend-signed lease (D8).
- **Localization gap:** any string without a translation falls back to Russian, then a neutral
  glyph state — never raw English in production.

## 8. Testing Strategy

- **Player auth:** PIN hash/verify; lockout after N failures; QR single-use/expiry; player token
  cannot satisfy any staff `Require*Permission` endpoint (negative authorization test); token
  bound to device/branch.
- **Self-only wallet read:** token for player A cannot read player B's `wallet-summary`; returns
  the caller's own balance/debt.
- **Self-start eligibility:** member with sufficient balance starts (lease issued, unlock
  dispatched); member below minimum-billable is rejected into the top-up flow; idempotent.
- **Self-extend ownership:** only the owning player's token can extend that session; wallet/limit
  coverage enforced; reuses the existing extend/lease-refresh path.
- **Top-up request lifecycle:** request created `pending`; operator confirm runs existing
  `TopUpWallet` and flips to `confirmed`; no balance change before confirm; expiry path; gateway
  actor can confirm without shell changes.
- **Warning model:** typed warning serializes round-trip (extend `PlayerShellContractSerialization`
  tests); low-time/low-balance/credit-limit map to the right severities; no hardcoded English.
- **Grace-mode action:** an incoming credit-limit/low-time warning surfaces the actionable
  "top up to keep playing" panel; a confirmed top-up + self-extend avoids the lock; absent that,
  the backend hard-lock still proceeds (shell can't block it).
- **Theming:** branch theme renders; touch targets ≥ 44 px; warning contrast meets WCAG-AA.

## 9. Decomposition & Sequencing

One coherent feature (member self-service on the PC) in five separable units:

1. **Player auth at the PC** (credential fields + player token + login/logout) — foundation;
   align with the portal/notifications shared identity (D7).
2. **Member status view** (self-only `wallet-summary` + balance/time/debt panel + typed localized
   warnings) — depends on 1.
3. **Self-start & self-extend** (player-self session endpoints reusing counter-loop machinery) —
   depends on 1; eligibility ties to the counter-loop billing function.
4. **In-shell top-up v1** (`TopUpRequestEntity` + request endpoint + operator confirm surfacing) —
   depends on 1; gateway-ready (D5).
5. **Theming & grace-mode UX** (branded shell + actionable "top up to keep playing" wired to
   auto-protection warnings) — depends on 2 and 4, and on the counter-loop warning push.

Localization of all shell strings is a cross-cut delivered alongside 2–5 via the localization spec.

## 10. Future (v2 / other tracks)

- **Real payment gateway** for unattended top-up (card / e-wallet auto-capture) — the flagged
  dependency that turns §5.4 fully self-service (D5).
- **Postpaid self-start within a credit limit** for trusted members (needs the counter-loop
  credit-limit machinery).
- **Member self-checkout from the PC** (the counter-loop spec's deferred Track-3 item) — end and
  settle one's own tab at the seat.
- **Loyalty / rewards, social presence, friends-on-seats, reservations from the PC** — ggLeap-style
  engagement once the identity and wallet rails are in place.
- **Customer-portal parity** (remote top-up, history, profile) sharing this player identity.
