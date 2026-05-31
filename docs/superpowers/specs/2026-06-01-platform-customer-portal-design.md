# Customer Self-Service Portal — Off-the-PC Player App (Track 3)

- **Date:** 2026-06-01
- **Status:** Design (decisions proposed; pending founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** customer shell experience ([[2026-06-01-platform-customer-shell-experience-design]]),
  notifications backbone ([[2026-06-01-platform-notifications-backbone-design]]),
  localization ([[2026-06-01-platform-localization-design]]),
  counter loop / postpaid checkout ([[2026-06-01-platform-counter-loop-postpaid-checkout-design]])

## 1. Context & Problem

Today the platform has **no customer-facing surface of any kind off the PC**. Verified:

- **`AFK4.Platform.Web` is staff-only** (platform-admin + owner/operator). There is no player web
  or mobile app anywhere in the repo.
- **Every wallet, session, reservation, and POS endpoint is staff-protected.** Authentication is an
  **opaque bearer token** (`OpaqueStaffTokenService`: a `{guid}.{secret}` string, SHA-256–hashed at
  rest, validated by `StaffAuthenticationMiddleware`; **no JWT**). A parallel
  `PlatformAdminAuthenticationMiddleware` exists for platform staff. There is **no anonymous/public
  middleware branch and no player principal**.
- **No rate limiting** is configured anywhere in `Program.cs` (no `AddRateLimiter`).
- **`PlayerAccountEntity`** holds only `DisplayName`, `PhoneNumber`, `IsActive`, `HomeBranchId`,
  `OrganizationId`, `CreatedAtUtc`. It has **no credential, no contact-verification, no preference
  fields** — a player cannot log in. Wallet balance and debt are **derived from
  `LedgerEntryEntity`** (per-player, per-currency), not stored on the account.
- **Reservations already exist but are staff-only** (`ReservationDto`, `CreateReservationRequest`,
  `EfReservationService`). Notably `ReservationSourceNames` **already defines `Online`** alongside
  `Operator`, and `ReservationStateNames` is `pending → confirmed → seated → cancelled` — the data
  model anticipated a customer channel that was never built.
- **No payment gateway exists.** `PaymentMethodNames` is only `cash` and `card_manual`; the lone
  provider is `ManualPaymentProvider`. Money cannot be collected online today.

A customer who wants to check their balance, see what they spent last visit, book a seat for tonight,
or top up before arriving has **no option but to ask the operator at the counter**. This spec designs
the **customer self-service portal**: a mobile-first app, off the PC, with its own public API surface
and its own player authentication, sharing one `PlayerAccount` identity with the in-shell experience.

## 2. Goals

1. **Player authentication** distinct from staff auth — a player principal, public sign-in, sessioned
   token, reusing the existing opaque-token and password-hashing primitives.
2. **Dashboard**: wallet balance, active session (seat + live elapsed/remaining time + live accrued
   cost when a session is running), outstanding debt.
3. **History & receipts**: past visits/sessions and POS purchases, each with a viewable receipt.
4. **Wallet top-up** (see D5 — v1 is "request top-up / pay at counter"; online charge is gated on a
   real gateway).
5. **Reservation / booking** of a seat for a future time, from the customer side (`source = online`).
6. **Account self-service**: phone number, password/PIN, marketing opt-in, preferred language.
7. A **new public API surface** (`/api/public/*` for unauthenticated, `/api/me/*` for the
   authenticated player), **rate-limited**, isolated from the staff surface.

### Non-goals (deferred / owned elsewhere)

- **In-PC shell** experience: self-login on the gaming PC, in-session top-up prompt, "time's up"
  overlays — owned by the customer-shell spec. This portal is strictly **off the PC**.
- **SMS/email delivery** (OTP codes, receipt emails, booking confirmations) — owned by the
  notifications backbone. This spec **consumes** that channel and degrades gracefully without it.
- **Translation catalogue / locale resources** — owned by the localization spec. This portal stores
  the player's `PreferredLanguage` and honours it.
- **Native iOS/Android apps** — v1 is a responsive PWA (see D1); native is future.
- **Real online payment / gateway integration** — explicitly out of v1 (see D5); the portal is built
  so the gateway slots in behind the existing `IPaymentProvider` abstraction later.
- **Loyalty, referrals, tournaments, social** — future product surface.

## 3. Proposed Decisions

These are forks the founder reviews. Defaults chosen for fastest safe path to value.

| # | Decision | **Proposed choice** | Rationale / fork |
|---|----------|---------------------|------------------|
| D1 | Delivery vehicle | **Responsive PWA (web first)**, installable; native later | One codebase, instant updates, no app-store latency; push later via web-push or native shell. Fork: native-first if push/biometrics are must-have at launch. |
| D2 | Player auth method | **Phone number + OTP** as primary, with an **optional password/PIN** the player can set for faster re-login | Phone is the identity the club already collects; OTP needs no remembered secret. **Depends on the notifications backbone** for SMS/email OTP delivery. Fork until SMS lands: **password/PIN-only** login (operator sets an initial PIN at the counter), OTP enabled when delivery is ready. |
| D3 | Token format | **Opaque hashed bearer token**, mirroring `OpaqueStaffTokenService` (new `PlayerAccessToken` / `PlayerRefreshToken` tables) | Reuses a proven, revocable pattern; no JWT key management; instant server-side revocation. Shorter access lifetime than staff (see §5.2). |
| D4 | Booking model | **Request-to-confirm** (player creates `pending`; operator/auto-rule confirms → `confirmed`) for v1 | Safer with no deposit/payment; reuses existing reservation states. Fork: **instant-confirm** for branches that opt in (seat held immediately) once capacity rules and no-show handling are trusted. |
| D5 | Online top-up | **v1 = "Top up at counter" only**: portal shows balance/history and lets the player **request** a top-up amount (creates a pending intent the operator fulfils); **no online charge** | No gateway exists today (`cash`/`card_manual`/`ManualPaymentProvider`). Build the wallet/top-up UI and a `PaymentIntent` seam now; wire a real gateway (`IPaymentProvider`) in a later track. **Flagged:** self-pay top-up is NOT in v1. |
| D6 | Identity sharing | **One `PlayerAccount`** for portal + shell + counter; credentials are **login primitives on the player identity**, not a separate user | A player who exists at the counter can claim the portal; the shell and portal authenticate the same identity. |
| D7 | Multi-branch / multi-tenant | Player is scoped to its **`OrganizationId` + `HomeBranchId`**; portal is **branded per tenant**, resolved by a public tenant key (same mechanism staff use via `sign-in-by-tenant-key`) | Keeps tenant isolation identical to the rest of the platform. |
| D8 | Verification gating | Wallet/booking actions require a **verified phone**; read-only dashboard is allowed once authenticated | Limits abuse of booking/top-up by unverified accounts. |

## 4. Architecture Overview

The portal is a **new public edge** in front of the existing Platform.Api, with its own
authentication middleware and a **rate-limited** public route group. It never touches the staff
surface; it reuses the same domain services (billing/ledger reads, reservations) behind a
**player-scoped authorization** check that pins every query to the authenticated `PlayerAccountId`.

```
 Customer phone/browser (PWA, mobile-first)
        │  HTTPS
        ▼
 ┌──────────────────────────────────────────────────────────────┐
 │ Platform.Api                                                   │
 │                                                                │
 │  RateLimiter (per-IP + per-account)  ── public + me groups     │
 │        │                                                       │
 │  /api/public/*  (anonymous)                                    │
 │    ├─ tenant branding lookup (by tenant key)                   │
 │    ├─ POST player/otp/request ─▶ Notifications backbone (SMS)  │
 │    ├─ POST player/otp/verify  ─┐                               │
 │    └─ POST player/sign-in     ─┴▶ PlayerCredentialService      │
 │                                   + PlayerTokenService ─▶ token │
 │        │                                                       │
 │  PlayerAuthenticationMiddleware  (Bearer → PlayerContext)      │
 │        │ pins PlayerAccountId on every /api/me/* request       │
 │  /api/me/*  (player-scoped)                                    │
 │    ├─ GET  dashboard ───▶ Ledger reads (balance/debt) +        │
 │    │                      active SessionDto (live cost §CL)    │
 │    ├─ GET  history/receipts ─▶ Sessions + PosSales + Receipts  │
 │    ├─ POST wallet/top-up-intent ─▶ PaymentIntent (pending)     │
 │    ├─ GET/POST reservations ──▶ EfReservationService (online)  │
 │    └─ GET/PATCH profile ──────▶ PlayerProfileService           │
 └──────────────────────────────────────────────────────────────┘
            │ same PlayerAccount identity
            ▼
 In-PC customer shell (separate spec) — self-login, in-session top-up
```

Six components, each independently testable:

1. **Player identity & credentials** (entities + `PlayerCredentialService`, shared with the shell).
2. **Player authentication edge** (`PlayerTokenService` + `PlayerAuthenticationMiddleware` + public
   sign-in/OTP endpoints + rate limiter).
3. **Dashboard read** (balance/debt from ledger + active session projection).
4. **History & receipts read** (player-scoped session/POS/receipt projections).
5. **Wallet top-up intent** (pending intent + operator-fulfil path; gateway seam stubbed).
6. **Reservations (online) + profile self-service** (reuse reservation service; new profile service).

## 5. Components

### 5.1 Player identity & credentials

`PlayerAccountEntity` stays the single identity. Credentials and contact-verification live in a
**separate `PlayerCredentialEntity`** (one-to-one) so the account record stays clean and a player can
exist (counter-created) before they ever claim portal access:

```csharp
public sealed class PlayerCredentialEntity
{
    public Guid PlayerCredentialId { get; set; }
    public Guid PlayerAccountId { get; set; }   // 1:1 with PlayerAccountEntity
    public Guid OrganizationId { get; set; }
    public string? PasswordHash { get; set; }    // nullable: OTP-only players have none
    public bool PhoneVerified { get; set; }
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }
    public int FailedLoginCount { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

Preference fields go on the player account (read everywhere): add
`PreferredLanguage` (string, e.g. `ru`/`tg`/`en`), `MarketingOptIn` (bool, default `false`).

**Password hashing reuses the existing primitive.** The staff/admin side already has
`PasswordHashingStaffCredentialService` / `PasswordHashingPlatformAdminCredentialService`. Extract the
hashing/verification into a shared `IPasswordHasher` (if not already shared) and build
`PlayerCredentialService` on it. **No new crypto.**

**OTP** (when notifications backbone is live): a short-lived, single-use code in a
`PlayerOtpChallengeEntity` (hashed code, `ExpiresAtUtc`, `AttemptCount`, `Purpose` =
`sign_in | verify_phone`). The challenge is delivered via the notifications backbone; this spec only
**creates and verifies** challenges.

### 5.2 Player authentication edge

Mirror the staff pattern, **separate tables and middleware** so the surfaces never cross:

- `PlayerAccessTokenEntity`, `PlayerRefreshTokenEntity` — same shape as `StaffAccessTokenEntity`
  (SHA-256 `TokenHash`, `ExpiresAtUtc`, `RevokedAtUtc`), scoped by `PlayerAccountId` + `OrganizationId`.
- `PlayerTokenService` (mirrors `OpaqueStaffTokenService`): `IssueAsync`, `RefreshAsync`,
  `ValidateAsync`. **Access-token lifetime shorter than staff** — **Proposed: 1 hour access /
  30-day refresh** (staff is 8h/30d; customer devices are less trusted).
- `PlayerAuthenticationMiddleware`: reads `Authorization: Bearer`, validates, sets a
  `PlayerContext { PlayerAccountId, OrganizationId, PhoneVerified }` on a `IPlayerContextAccessor`.
  Runs **only** for `/api/me/*`; the staff/admin middlewares are untouched.

**Authorization invariant:** every `/api/me/*` handler resolves data **only** for
`PlayerContext.PlayerAccountId`. No route accepts a player id from the caller — it always comes from
the token. This is the core isolation guarantee.

**Public endpoints** (anonymous, in `/api/public/*`):

| Endpoint | Purpose |
|---|---|
| `GET  /api/public/tenant/{tenantKey}/branding` | logo/name/locale for the portal shell |
| `POST /api/public/player/otp/request` | start OTP challenge (phone) — rate-limited hard |
| `POST /api/public/player/otp/verify`  | verify code → issue tokens (or mark phone verified) |
| `POST /api/public/player/sign-in`     | password/PIN sign-in → issue tokens |
| `POST /api/public/player/refresh`     | rotate tokens (mirrors staff refresh) |

**Rate limiting (new):** introduce `AddRateLimiter` (first use in the codebase). Two policies:
a strict **per-IP fixed-window** on `/api/public/*` (OTP request/verify and sign-in are abuse magnets),
and a looser **per-account** policy on `/api/me/*`. Account lockout (`FailedLoginCount`/`LockedUntilUtc`)
backstops credential stuffing. **Proposed defaults:** OTP request ≤ 1/30s and ≤ 5/hour per phone;
sign-in ≤ 10/min per IP; `/api/me/*` ≤ 60/min per account.

### 5.3 Dashboard read

`GET /api/me/dashboard` → `PlayerDashboardDto`:

- **Wallet balance** and **debt**: derived from `LedgerEntryEntity` for this player (sum by
  `AccountType`/`EntryType`, per currency), reusing the existing ledger read path — **not** a stored
  field. Returned as `long` minor units + `currencyCode`.
- **Active session** (if any): seat name, `startedAtUtc`, duration mode, remaining time (fixed) and
  **live accrued cost** — reuse the `accruedCostMinorUnits` projection introduced by the counter-loop
  spec (`TariffBilling.ComputeAmount`), so portal and counter never disagree. The client ticks the
  amount locally between refreshes (same approach as the operator UI).
- All money is `long` minor units end-to-end; convert to major only at the UI boundary
  (`minorToMajor` before `formatCurrency`, per the established convention).

### 5.4 History & receipts read

- `GET /api/me/visits?cursor=` → paginated past sessions for this player: seat, start/end, duration,
  time charge, attached POS total, grand total, currency.
- `GET /api/me/visits/{sessionId}/receipt` → the receipt document already produced at checkout
  (numbered via the existing `ReceiptNumberGenerator`), filtered to this player. Reuses the receipt
  the counter-loop unified checkout generates; the portal renders, never re-computes.
- `GET /api/me/purchases?cursor=` → standalone POS sales linked to this player (`PosSale.PlayerAccountId`,
  which already exists per migration `AddPosSalePlayerAccount`).

Cursor pagination keeps the mobile list cheap. All projections are **player-scoped** by token.

### 5.5 Wallet top-up (D5 — request/counter for v1)

No online charge in v1. Introduce a **`PaymentIntentEntity`** seam now so the gateway drops in later:

```csharp
public sealed class PaymentIntentEntity
{
    public Guid PaymentIntentId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public long AmountMinorUnits { get; set; }     // long minor units
    public string CurrencyCode { get; set; } = "TJS";
    public string Purpose { get; set; } = "wallet_topup";
    public string State { get; set; } = "pending";  // pending | fulfilled | cancelled | expired
    public string Method { get; set; } = "counter";  // counter (v1) | gateway (future)
    public Guid? FulfilledByLedgerEntryId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset? FulfilledAtUtc { get; set; }
}
```

- `POST /api/me/wallet/top-up-intent` → player picks an amount; creates a `pending` intent and (via
  notifications) optionally pings the branch. The portal shows a code/QR the operator scans, or the
  operator sees it in their pending-intents list.
- The **operator fulfils** it at the counter (cash/card via the existing manual flow), which writes
  the **wallet credit ledger entry** and flips the intent to `fulfilled`. The wallet credit goes
  through the **existing billing/ledger write path** — the portal never writes the ledger directly.
- **Gateway-ready seam:** when a real provider lands, `Method = gateway` + `IPaymentProvider` confirms
  the charge and the same fulfil step runs. **Flagged:** building this seam is cheap; wiring a live
  gateway is a separate track and explicitly out of v1.

**Add `Wallet` to `PaymentMethodNames`** is owned by the counter-loop spec; this spec just reads the
resulting balance.

### 5.6 Reservations (online) + profile self-service

**Reservations** reuse `EfReservationService` and the existing contracts — no new domain model:

- `GET  /api/me/reservations` → this player's reservations.
- `POST /api/me/reservations` → maps to `CreateReservationRequest` with **`Source = online`** (already
  defined in `ReservationSourceNames`) and the player's own `PlayerAccountId`/`CustomerName`/`PhoneNumber`
  injected from the token (the player cannot book *as* someone else).
- `DELETE /api/me/reservations/{id}` → player-initiated cancel (→ `cancelled`).
- **D4 request-to-confirm:** a player booking lands in `pending`; an operator (or an opt-in
  auto-confirm rule) moves it to `confirmed`. The portal shows the state and notifies on
  confirm/cancel via the notifications backbone. **Proposed:** branches may opt into instant-confirm
  later; v1 default is request-to-confirm with no deposit.
- **Edge cases:** seat double-booking is prevented by the existing reservation overlap checks in
  `EfReservationService` (verify and extend if the current check is operator-only). No-show handling
  is a future policy (auto-cancel after a grace window).

**Profile** — new lightweight `PlayerProfileService`:

- `GET  /api/me/profile` → display name, phone (+ verified flag), `preferredLanguage`, `marketingOptIn`.
- `PATCH /api/me/profile` → update `preferredLanguage` and `marketingOptIn` freely; **changing the
  phone number re-triggers OTP verification** (sets `PhoneVerified = false` until re-verified).
- `POST /api/me/profile/password` → set/change password/PIN (reuses `PlayerCredentialService`;
  requires current credential or a fresh OTP).

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `PlayerAccountEntity` | add `PreferredLanguage` (string), `MarketingOptIn` (bool, default false) |
| `PlayerCredentialEntity` | **new** — 1:1 with player; `PasswordHash?`, `PhoneVerified`, lockout fields |
| `PlayerOtpChallengeEntity` | **new** — hashed code, purpose, expiry, attempts (gated on notifications) |
| `PlayerAccessTokenEntity`, `PlayerRefreshTokenEntity` | **new** — mirror staff token tables (hashed) |
| `PaymentIntentEntity` | **new** — wallet top-up intent (counter-fulfilled in v1, gateway-ready) |
| `IPasswordHasher` | extract/reuse from existing `PasswordHashing*CredentialService` (no new crypto) |
| `PlayerTokenService` | **new** — mirrors `OpaqueStaffTokenService` (issue/refresh/validate) |
| `PlayerAuthenticationMiddleware`, `PlayerContext`, `IPlayerContextAccessor` | **new** — `/api/me/*` only |
| `PlayerCredentialService`, `PlayerProfileService` | **new** — sign-in, OTP, profile |
| Rate limiter | **new** — `AddRateLimiter`; per-IP public policy + per-account `me` policy |
| Public contracts | `PlayerSignInRequest/Response`, `PlayerOtpRequest/Verify`, `PlayerDashboardDto`, `PlayerVisitDto`, `PlayerProfileDto`, `TopUpIntentRequest` (new contract set) |
| New endpoint group | `/api/public/*` (anonymous) and `/api/me/*` (player-scoped) |
| `ReservationSourceNames.Online` | **already exists** — reused, no change |
| `PosSale.PlayerAccountId` | **already exists** — reused for purchase history |

Each new entity carries an EF migration. Money stays `long` minor units end-to-end; UI converts at the
boundary only. The staff/admin auth surface is **not modified** — the player edge is strictly additive.

## 7. Error Handling & Edge Cases

- **Token isolation:** a player token presented to a staff route fails (different middleware/table);
  a staff token on `/api/me/*` fails. No cross-acceptance.
- **Caller-supplied player id is never trusted** — always taken from `PlayerContext`. Attempting to
  read another player's session/receipt returns 404 (not 403, to avoid existence disclosure).
- **OTP abuse:** hard per-phone and per-IP rate limits; single-use, short-lived codes; capped
  `AttemptCount`; generic "code sent if the number is registered" responses to avoid enumeration.
- **Credential stuffing:** `FailedLoginCount` + `LockedUntilUtc` lockout; per-IP sign-in rate limit.
- **Notifications backbone unavailable:** OTP-dependent flows are disabled and the portal falls back
  to **password/PIN sign-in** (operator-set initial PIN). The portal must **not** hard-fail when SMS
  is down — it degrades to counter-assisted onboarding.
- **No payment gateway:** top-up is request-only; the portal never claims an online charge succeeded.
  A pending intent that is never fulfilled **expires** (e.g. 24h) and is clearly shown as expired.
- **Unverified phone (D8):** booking and top-up-intent require `PhoneVerified == true`; dashboard and
  history are allowed. Clear, recoverable prompts to verify.
- **Currency:** balance/history are per the player's ledger currency; mixed-currency is rejected
  upstream (existing ledger guard). The portal displays a single currency.
- **Concurrent reservation:** existing overlap/capacity checks in `EfReservationService` apply; a
  losing request gets a clear "seat no longer available" error.
- **Stale active-session cost:** the live accrued cost is a projection; the authoritative charge is
  the counter checkout. The portal labels it "estimated running cost".
- **Multi-device sessions:** refresh-token rotation revokes the prior refresh token; a stolen access
  token is short-lived (1h) and revocable server-side.

## 8. Testing Strategy

- **Auth edge:** issue → validate → refresh → revoke for player tokens; staff token rejected on
  `/api/me/*` and vice-versa; expired/revoked tokens rejected.
- **Authorization isolation:** player A cannot read player B's dashboard/visits/receipt/reservation
  (404); every `/api/me/*` handler ignores any caller-supplied id.
- **OTP:** request creates a single-use, expiring challenge; verify issues tokens / verifies phone;
  reuse, expiry, and over-attempt are rejected; rate limits enforced.
- **Credential lockout:** N failed sign-ins lock the account until `LockedUntilUtc`.
- **Rate limiting:** public OTP/sign-in policies and the per-account `me` policy return 429 past
  threshold; first use of `AddRateLimiter` is integration-tested.
- **Dashboard/history:** balance and debt match the ledger sums; active-session cost equals
  `TariffBilling.ComputeAmount`; pagination cursors are stable.
- **Top-up intent:** creates `pending`; operator fulfil writes exactly one wallet-credit ledger entry
  and flips state to `fulfilled`; expiry path works; no double-credit on retry (idempotent).
- **Reservations:** online booking lands `pending` with `Source = online` and the token's player;
  cannot book as another person; overlap rejected; confirm/cancel transitions and notifications fire.
- **Profile:** language/marketing update freely; phone change resets `PhoneVerified` and re-triggers OTP.
- **Degradation:** with notifications disabled, password/PIN sign-in still works end-to-end.

## 9. Decomposition & Sequencing

Build order for the implementation plan (each unit independently shippable behind the public edge):

1. **Player identity & credentials** — `PlayerCredentialEntity`, account preference fields, shared
   `IPasswordHasher`, `PlayerCredentialService` (password/PIN path; OTP stubbed). Foundation.
2. **Player auth edge** — token tables, `PlayerTokenService`, `PlayerAuthenticationMiddleware`,
   `/api/public/*` sign-in/refresh, **rate limiter**. Depends on 1.
3. **Dashboard read** — balance/debt + active-session projection. Depends on 2 and the counter-loop
   accrued-cost field.
4. **History & receipts** — visits/purchases/receipt endpoints. Depends on 2.
5. **Profile self-service + OTP** — profile endpoints; OTP wired once the notifications backbone lands
   (until then, password/PIN only). Depends on 1, 2, and notifications.
6. **Reservations (online) + wallet top-up intent** — reuse reservation service with `Source = online`;
   `PaymentIntentEntity` + operator-fulfil path. Depends on 2; top-up depends on the counter `Wallet`
   payment method.

The **PWA frontend** (new `AFK4.Customer.Web` or similar) is a parallel track that consumes these
endpoints; it is mobile-first, localized (localization spec), and branded per tenant (D7).

## 10. Future (v2 / other tracks)

- **Real online top-up:** integrate a payment gateway behind `IPaymentProvider`; `PaymentIntent.Method
  = gateway`; webhook-confirmed wallet credit. The seam is built in v1; the gateway is the only
  missing piece.
- **Native iOS/Android** apps (or a native push shell over the PWA) for push notifications and
  biometric login.
- **Instant-confirm bookings** with optional deposits and no-show policies (D4 opt-in).
- **Loyalty / referrals / tournaments** customer surface.
- **In-PC shell convergence:** the shell self-login and in-session top-up reuse the same
  `PlayerCredentialService` and `PlayerTokenService` defined here (customer-shell spec).
- **Receipt email / PDF** delivery via the notifications backbone.
