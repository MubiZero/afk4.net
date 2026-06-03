# Customer Shell — Full Self-Service (Implementation Reconciliation)

- **Date:** 2026-06-03
- **Status:** Design — approved, ready for plans
- **Scope owner:** Platform (AFK4)
- **Supersedes/updates:** `2026-06-01-platform-customer-shell-experience-design.md`
  (the vision). This document is the **build-ready reconciliation** of that spec against
  what the customer-portal, notifications, and localization tracks already landed on `main`,
  plus the two external integrations that turned out to exist (`dcgate`, an SMS gateway).
- **Related:** customer-portal PWA (`2026-06-03-customer-portal-pwa-design.md`),
  counter-loop (`2026-06-01-platform-counter-loop-postpaid-checkout-design.md`),
  notifications-backbone, localization.

## 1. Why this document exists

The original shell spec (2026-06-01) was written before the customer-portal track shipped.
Since then a large part of the player-facing backend already landed on `main` via PR #49 (portal)
/ #46 (localization) / #47 (notifications). This spec records **what to reuse**, **what is genuinely
new**, and the **decisions** made during the 2026-06-03 brainstorm, so the shell can be built without
re-deriving the vision.

The headline change vs the original spec: **online self-payment is no longer blocked.** A working
DC-Bank payment gateway (`dcgate`) is already deployed in production, and an SMS gateway exists — so
true self-top-up and SMS-OTP self-registration are in scope, not deferred.

## 2. Ground truth — already on `main` (REUSE, do not rebuild)

Verified against current code 2026-06-03:

- **Player auth:** opaque player token (`OpaquePlayerTokenService`), `PlayerContext`
  (`PlayerAccountId`, `OrganizationId`, `PhoneVerified` — **no device/branch binding**),
  `PlayerAuthenticationMiddleware` on `/api/me/*`, rate limiters `player-public` / `player-me`.
- **Operator sets PIN:** `POST /api/branches/{branchId}/players/{playerId}/pin`
  (`PlayerCredentialService.SetPasswordAsync`, perm `players.create`).
- **Wallet read:** `GET /api/me/dashboard` → `PlayerDashboardDto { WalletBalance, DebtBalance,
  ActiveSession? }` (built on `LedgerBalanceProjector.GetWalletSummaryAsync`).
- **Top-up:** `PaymentIntentEntity` (`PlayerAccountId, OrganizationId, BranchId, AmountMinorUnits,
  CurrencyCode, Purpose, State pending|fulfilled|cancelled|expired, Method, FulfilledByLedgerEntryId`);
  player `POST /api/me/wallet/top-up-intent` + `GET /api/me/wallet/top-up-intents`; operator
  `POST /api/wallet/top-up-intents/{id}/fulfil` (perm `TopUpWallet`, idempotency key = PaymentIntentId).
  **No separate `TopUpRequestEntity` — `PaymentIntent` is the gateway-ready entity.**
- **Branding:** `GET /api/public/tenant/{slug}/branding` → `{ OrganizationId, Name, LogoUrl?, AccentColor? }`.
- **Eligibility math:** `TariffBilling.ComputeForMinutes / ComputeForElapsed` (pure, minimum-billable + rounding).
- **WPF localization is wired:** `AFK4.Localization` + `AFK4.Localization.Wpf` (`{loc:T}` XAML extension,
  JSON catalogs ru/en/tg, `FormatCurrency`, fallback chain). The shell already routes its warning string
  through `{loc:T Key=shell.warning.attention}` — **the spec's "hardcoded English" problem is already solved.**
- **Device command `warn`** already exists in `DeviceCommandTypeNames` (from counter-loop), but the shell
  does not handle it yet.
- **WPF formatters:** `RemainingTimeFormatter` (HH:MM:SS / MM:SS), `ILocalizationService.FormatCurrency`.
- **Shell today:** `MainWindow.xaml` + `PlayerShellViewModel` only; **no HTTP client** (named-pipe IPC only);
  `.csproj` references `Shared.Contracts` + `Localization` + `Localization.Wpf`.

What is **NOT** there: any player-token session start/extend; any online payment gateway client; any login /
wallet / top-up UI in the shell; a typed warning model; branding transport to the shell.

## 3. Decisions (brainstorm 2026-06-03)

| # | Decision | Choice |
|---|----------|--------|
| A | Player API surface + token | **Reuse `/api/me/*` + existing org-scoped token.** Self-start/extend are added under `/api/me/*`. No `/api/player-self/*`. |
| B | Device-bound token | **Skipped.** Token lives in shell memory only, dropped on lock/idle/sign-out; device-binding is defense-in-depth we can add later. |
| C | `PhoneVerified` gate on top-up | **Dropped** from `POST /api/me/wallet/top-up-intent`. Money only moves on confirmation (operator or dcgate webhook), so the gate added friction without protection. Affects PWA too (acceptable). |
| D | Online self-payment | **Real, via `dcgate`** (DC-Bank). Operator-confirm stays as fallback. `PaymentIntent.Method ∈ {dcgate, counter}`. |
| E | QR login | **In scope.** |
| F | OTP self-registration | **In scope, real**, via the existing SMS gateway. Sequenced last; shell core does not wait on it (operator sets PIN until then). |
| G | Loyalty / bar ordering / tournaments | **Out of scope** — separate epics after the shell core. |
| H | Self-start tariff | One member tariff → use it silently; multiple → simple chooser in shell. No complex pickers in v1. |
| I | Branding transport | Agent puts branding into `PlayerShellStateDto` (IPC), so the shell needs no extra HTTP/slug resolution for theming. |

## 4. External integration: dcgate (DC-Bank payment gateway)

`dcgate` (repo `MubiZero/dcgate`, prod `https://dcgate.mubi.dev`) creates DC payment links with unique
18-char comments, listens to "DC Next Bot" Telegram notifications, matches incoming bank messages to
pending payments, and fires webhooks. It is multi-tenant by **project API key**.

**Consumer contract (AFK4 → dcgate):**
- Auth: `Authorization: Bearer <PROJECT_API_KEY>`.
- `POST /api/payments` body `{ amount: "<major.units string>", externalOrderId: "<≤128 chars>", metadata?: {} }`
  → `{ paymentId, status, amount, currency, comment, expiresAt, payUrl }`.
  `payUrl` is a DC link (`http://pay.dc.tj/?A=<card>&s=<amount>&c=<comment>&...`).
- `GET /api/payments/{id}` → same shape (status poll).

**Webhook (dcgate → AFK4):** `POST <project webhookUrl>` JSON
`{ eventId, eventType: payment.paid|payment.disputed|payment.expired, payment: { id, amount, comment,
currency, externalOrderId, paidAt?, status }, projectId }`.
Headers: `x-dcgate-event-id`, `x-dcgate-event-type`, `x-dcgate-project-id`, and (if a webhook secret is
set) `x-dcgate-signature: sha256=<HMAC-SHA256(rawBody, secret)>`.

**AFK4 mapping:**
- On player top-up online → AFK4 creates a `PaymentIntent` (`Method=dcgate`, pending) **and** calls dcgate
  `POST /api/payments` with `amount` = minor→major of the intent, `externalOrderId = PaymentIntentId`,
  `metadata = { playerAccountId, branchId }`. Store the returned `payUrl`/`comment` on the intent (new
  nullable columns) and return `payUrl` to the shell.
- Shell renders `payUrl` as a **QR**; the player pays from their own bank app (no card entry in the shell).
- On `payment.paid` webhook → verify HMAC, dedupe by `eventId` (idempotent), find the intent by
  `externalOrderId`, run the existing `TopUpWalletAsync` (idempotency key = PaymentIntentId), mark the
  intent `fulfilled`. `payment.expired` → mark intent expired; `payment.disputed` → flag for operator.
- Money units: dcgate uses major-unit numeric strings; AFK4 stays `long` minor units. Convert only at the
  dcgate boundary. Currency is TJS on both sides.

**Operational dependency (not code):** register AFK4 as a dcgate project with
`webhookUrl = https://<afk4-api>/api/public/payments/dcgate/webhook`; obtain the project API key + webhook
secret; store them in AFK4 secrets. To be collected at Unit 2 implementation.

## 5. New backend & shell work, by unit

### Unit 1 — Backend core (Linux-testable)
- `POST /api/me/sessions/start` body `{ deviceId, tariffRuleVersionId?, idempotencyKey }`:
  resolve player from token; resolve seat from `deviceId` (**verify the device→seat mapping exists at
  plan time** — if there is no direct link, the shell sends `seatId` from IPC state and the backend
  validates it belongs to the device's branch); tariff = supplied or the seat/branch default
  member tariff; eligibility = wallet covers the minimum-billable charge (`TariffBilling`); on fail →
  recoverable `409 insufficient_balance` (shell pivots to top-up). On success reuse the operator
  `SessionCommandService` prepaid-wallet start, issue lease, dispatch `unlock`. Shell flips to active only
  on backend-confirmed IPC state (trust boundary).
- `POST /api/me/sessions/{sessionId}/extend` body `{ minutes, idempotencyKey }`: token owns the active
  session; wallet covers the extension; reuse the existing extend → lease-refresh path.
- Drop the `PhoneVerified` gate on `POST /api/me/wallet/top-up-intent`.
- `GET /api/branches/{branchId}/wallet/top-up-intents?status=pending` (perm `TopUpWallet`) for the operator surface.
- Extend `PlayerShellStateDto`: add `WarningKind` (`none|low_time|low_balance|credit_limit|connectivity`);
  make `WarningThresholdSeconds` configurable (drop the literal `300` in `Worker.CreatePlayerShellState`);
  add a nullable `Branding` (club name / logo url / accent color) the agent fills. Update
  `PlayerShellContractSerialization` round-trip tests.

### Unit 2 — dcgate integration (Linux-testable)
- `IDcGateClient.CreatePaymentAsync` (typed HTTP client; base URL + API key from config/secrets).
- Top-up intent creation optionally creates a dcgate payment and returns `payUrl`/`comment` (new nullable
  columns on `PaymentIntentEntity` + migration; `Method=dcgate`).
- `POST /api/public/payments/dcgate/webhook`: HMAC verify, idempotent by `eventId`, credit via
  `TopUpWalletAsync`, transition intent. Rate-limited (`player-public`-style).
- Config/secrets: dcgate base URL, project API key, webhook secret.

### Unit 3 — Operator web (Linux-testable)
- Floor-map seat tile shows "member requests +N TJS"; confirm action reuses `fulfil`; list from the Unit 1
  pending endpoint.

### Unit 4 — WPF shell (Windows-gated, built/tested on the `D:\` clone)
- Add an HTTP client to `AFK4.Player.Shell` for `/api/me/*` (Bearer player token, **in memory only**).
- Login view: phone/member + PIN pad (≥44×44 px, WCAG-AA) + QR-login affordance; token dropped on
  lock/idle/sign-out.
- Member home: balance / time remaining / debt (`FormatCurrency`, `RemainingTimeFormatter`), tariff list,
  localized warning states driven by `WarningKind`.
- Self-start / self-extend (optimistic "starting…/extending…", transition on backend state).
- Top-up panel: dcgate QR (`payUrl`) + "waiting for confirmation", operator-confirm fallback; refresh
  wallet on confirmation.
- Theming from `Branding`; grace-mode "top up to keep playing" wired to `WarningKind ∈ {low_time, credit_limit}`.
- Detailed HUD/login/grace visuals: design via `interface-limb` at implementation. Localization already wired.

### Unit 5 — OTP self-registration (Linux + SMS gateway)
- Player self-registers → SMS code → self-sets PIN. `ISmsSender` seam; the concrete SMS-gateway contract is
  pinned from the user's gateway details at this unit. Sequenced last; shell core works without it.

## 6. Decomposition & order

Each unit is its own TDD cycle (spec → plan → execute), no auto-merge. Recommended order:
**1 → 2 → 3 → 4 → 5.** Units 1–3 and 5 gate on `dotnet test` (Linux); Unit 4 gates on the Windows clone
(`D:\projects\afk4.net`, see env quirks). Backend stays `long` minor units end-to-end; convert to major
units only at UI / dcgate boundaries.

## 7. Trust & security (unchanged from the vision)

- Backend is the only authority. Self-start/extend/top-up are **requests**; the shell shows optimistic UI
  but only unlocks/continues on the backend-pushed `PlayerShellStateDto`. A tampered shell cannot mint a
  token, pass staff checks, or unlock without a backend-signed lease.
- Player token in shell memory only, never persisted, dropped on lock/idle/sign-out.
- dcgate webhook authenticated by HMAC + idempotent by `eventId`; money credited only through the existing
  audited `TopUpWalletAsync`.
- No card data ever entered in the shell (QR pay-from-own-bank-app).

## 8. Testing strategy

- **Unit 1:** self-start eligibility (covered → lease+unlock; below minimum → 409 into top-up; idempotent);
  self-extend ownership + coverage; gate removal; pending-list scoping; `PlayerShellStateDto` round-trip with
  `WarningKind`/threshold/`Branding`.
- **Unit 2:** dcgate client request shape (mocked HTTP); webhook HMAC verify (good/bad signature), idempotent
  replay, `payment.paid` credits once, `expired`/`disputed` transitions; minor↔major conversion.
- **Unit 3:** operator pending list renders; confirm runs `fulfil`; no balance move before confirm.
- **Unit 4 (Windows):** login PIN flow; balance/time/debt render; self-start/extend optimistic→confirmed;
  top-up QR shown; theming from `Branding`; grace panel on the right `WarningKind`; touch targets ≥44 px.
- **Unit 5:** SMS code issue/verify; lockout; self-set PIN; negative (player token can't hit staff endpoints).

## 9. Open operational dependencies (collect at the relevant unit)

- dcgate: AFK4 project registration + API key + webhook secret + confirmed prod base URL (Unit 2).
- SMS gateway: repo/endpoint + auth + send/verify contract (Unit 5).
