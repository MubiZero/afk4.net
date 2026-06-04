# Counter Loop — Postpaid Open-Tab & Unified Checkout (Track 1)

- **Date:** 2026-06-01
- **Status:** Design (approved decisions locked; pending spec review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], full-product UX audit (2026-06-01)

## 1. Context & Problem

The everyday core of the product is the **counter loop**: an operator starts a session
for a customer on a gaming PC, the customer plays, and the operator collects payment at the
end. Today this loop is modelled and surfaced in a way that fights its own purpose:

- **Postpaid is modelled like prepaid.** `StartGuestSessionRequest.DurationMinutes` is a
  required `int`; the session gets a fixed `EndsAtUtc = now + duration`. But postpaid in a real
  club is an **open tab**: the customer plays until they stop and pays for what they used. Asking
  for a duration up front is wrong for the mode and adds friction.
- **No live cost.** The operator never sees "this seat has accrued 45 TJS". The WPF seat panel
  hard-codes `MoneyImpactText => "0.00 TJS"`. There is no running-cost signal anywhere.
- **GUID entry.** In the WPF operator app the operator pastes a raw `PlayerAccountId` GUID and a
  raw `TariffVersionId` GUID. A search API and a tariff-options API already exist (the React
  operator UI uses them); the desktop app does not.
- **Checkout is split and invisible.** Ending a session (`POST /api/sessions/{id}/end`) shows no
  amount owed and offers no payment. POS sales (`PosSaleEntity`) have no link to a session, so
  time and snacks are two separate bills. Collecting the debt is a separate workspace and a
  separate manual step.
- **No automatic protection.** Nothing locks a PC when a fixed session's time is up, and there is
  no postpaid credit ceiling — an open tab could run unbounded with no way to stop it.

This design fixes the counter loop end-to-end so the operator runs and closes a postpaid session
in 2–3 taps with no GUIDs, sees cost accrue live, and collects one bill (time + snacks) in a
single action, with the PC locking automatically on a credit limit or a fixed time-out.

## 2. Goals

1. **Open-tab postpaid** sessions (no duration required), with a **fixed-duration** option retained.
2. **Live accrued cost** per active seat, shown on the floor map.
3. **Quick customer capture** (phone/name, one tap) and **search/dropdown pickers** for player and
   tariff — no raw GUID entry.
4. **Unified checkout**: time charge + attached POS sales = one bill, settled in one action with
   **split payment** (cash + card + wallet in one sale), then PC locks and a receipt is produced.
5. **Automatic protection**: warn-then-lock on fixed time-out and on a **postpaid credit limit**.

### Non-goals (explicitly deferred to other specs)

- Customer self-login on the PC, in-shell top-up, mobile/web customer portal (Track 3).
- Notification/email backbone — staff/owner invites, password reset, billing emails (Track 2).
- Offline operator fallback, billing outbox, configurable grace window (Tier-0 reliability spec).
- Anti-fraud preventive controls — refund/correction caps & approval, free-session hardening,
  actor-filtered audit (Tier-0 trust spec).
- Visual floor-map editor, owner revenue dashboards/CRM.

These are referenced where they border this work but are **not** implemented here.

## 3. Locked Decisions

| # | Decision | Choice |
|---|----------|--------|
| D1 | Session entry model | **Hybrid**: walk-in/postpaid for strangers via the counter; members with a wallet self-login on the PC (self-login itself is Track 3). |
| D2 | Postpaid time model | **Open tab by default**, with an optional fixed-duration mode. |
| D3 | Walk-in identity | **Always a quick account** — minimum phone (+ name), created in one tap at start. The anonymous "no-account" path stays only for the un-billed *guest* mode. |
| D4 | Where staff operate | **Desktop and browser** — live session control reaches parity in both. |
| D5 | Postpaid credit limit | **In scope, minimal**: one per-branch default + optional per-player override. |
| D6 | Split payment | **In scope for v1**: multiple payment parts in one checkout. |

## 4. Architecture Overview

The loop spans four surfaces. This design keeps the **backend as the single authority** for
session state, billing, and lock/unlock (consistent with the current architecture).

```
Operator (WPF / React)              Platform.Api                         Gaming PC
  floor map ── start ───────▶  SessionCommandService ── lease ───▶  Agent.Service ─▶ unlock
  seat tile (live cost ◀──── floor-map DTO accruedCost)            (heartbeat renews lease)
  POS cart ── sale(SessionId) ─▶ PosService (sale attached to tab)
  checkout ─ settle ────────▶  SessionCheckoutService ──┬─ time ledger charge (open-tab)
                                                          ├─ mark attached POS sales paid
                                                          ├─ split payments
                                                          └─ lock command ─▶ Agent.Service ─▶ lock
                              AutoProtectionService (hosted) ── warn/lock on limit or time-out
```

Five components, each independently testable:

1. **Open-tab session model** (contracts + `SessionCommandService` + `SessionBillingService`).
2. **Live accrued cost** (pure billing function + floor-map DTO field + client tick).
3. **Quick-account & pickers** (operator UI + existing player-search / tariff-options APIs + a
   minimal create-player-in-flow path).
4. **Unified checkout** (`PosSaleEntity.SessionId` link + new `SessionCheckoutService` + split
   payment contracts + `Wallet` payment method).
5. **Auto-protection** (credit-limit fields + a hosted background service issuing warn/lock).

## 5. Components

### 5.1 Open-tab session model

**Current state (verified):** `SessionEntity.EndsAtUtc` is already `DateTimeOffset?` (nullable) —
open-tab needs *no schema change here*. `StartGuestSessionRequest.DurationMinutes` is a required
`int`; `SessionBillingService.AppendStartLedgerEntriesAsync` writes the charge at **start**.

**Changes:**

- Add an explicit duration mode to the start request:

  ```csharp
  public sealed record StartGuestSessionRequest(
      Guid OrganizationId,
      Guid SeatId,
      string TariffRuleVersionId,
      string IdempotencyKey,
      string DurationMode = SessionDurationModes.Open,   // "open" | "fixed"
      int? DurationMinutes = null,                        // required when fixed
      Guid? PlayerAccountId = null,
      string BillingMode = "",
      Guid? TariffVersionId = null,
      Guid? PlayerPackageId = null);
  ```

  New `SessionDurationModes` constant set (`Open`, `Fixed`). `DurationMode = Fixed` requires a
  positive `DurationMinutes`; `Open` ignores/forbids it. Default is `Open` (postpaid's natural mode).

- In `SessionCommandService.StartGuestSessionAsync`:
  - `Open`: set `EndsAtUtc = null`. Lease is issued and renewed by heartbeat as today, with no end
    boundary while the session is `active`.
  - `Fixed`: set `EndsAtUtc = now + DurationMinutes` (current behaviour).

- **Billing split.** For **postpaid open-tab**, do *not* write a ledger charge at start. The charge
  is computed and written at checkout from elapsed time (§5.4). This requires:
  - `SessionBillingService` gains `Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(...)`
    and `Task AppendCheckoutLedgerEntriesAsync(...)` for the postpaid path.
  - `AppendStartLedgerEntriesAsync` is still used for **prepaid wallet / package** modes and for
    **fixed-duration** sessions where the amount is known up front. Open-tab postpaid is the only
    mode that defers to checkout.
  - Prepaid/wallet/package behaviour is otherwise unchanged.

**Edge cases:** an open-tab session that is force-ended with zero elapsed billable time produces a
zero charge (still goes through checkout). A fixed session reaching `EndsAtUtc` is handled by
auto-protection (§5.5), not by silent expiry.

### 5.2 Live accrued cost

**Pure function.** Extract the billable-amount calculation already in `EfTariffService`
(`billableMinutes = max(elapsed, MinimumBillableMinutes)`, rounded up to `RoundingIncrementMinutes`,
times `PricePerMinuteMinorUnits`, with overflow guards) into a reusable pure function
`TariffBilling.ComputeAmount(elapsed, tariffVersion)`. The same function powers both the live
display and the final checkout charge, so they never disagree.

**Transport.** Add `accruedCostMinorUnits` and `currencyCode` to the active-session shape in the
floor-map DTO (`SessionDto` / floor-map seat). The backend computes it from `StartedAtUtc → now`.

**Client.** The operator UI ticks the displayed amount locally between refreshes using the same
function and the seat's `StartedAtUtc` + tariff (mirrors the existing client-side countdown), and
reconciles on each floor-map refresh / SignalR update. The seat tile shows **timer ↑ and amount ↑**;
this replaces the hard-coded `MoneyImpactText => "0.00 TJS"` in `SeatContextPanelViewModel`.

Because the amount steps by the rounding increment, the live value jumps in increments — this is
correct (it equals what the customer will pay) and should be presented as such.

### 5.3 Quick-account & pickers

**Pickers (remove GUIDs).** Replace the raw `PlayerAccountIdText` and `TariffVersionIdText` inputs
in the WPF `SeatContextPanelViewModel` with:
- a **player search** field backed by the existing `POST /api/branches/{branchId}/players?query=`
  (min 2 chars; returns id, display name, phone, wallet/debt) — same as the React operator UI;
- a **tariff dropdown** backed by the existing `GET /api/branches/{branchId}/tariffs/options`
  (named tariffs with price/min/rounding).

This brings the desktop app to parity with the React operator app and removes the single biggest
ergonomic blocker.

**Create-in-flow (D3).** When no player matches, the operator captures **phone (+ optional name)**
and creates the account in one tap without leaving the start flow. Use the existing player-create
path if present; otherwise add `POST /api/branches/{branchId}/players` taking
`{ phoneNumber, displayName? }` and returning the new `PlayerAccountId`, audited as a staff action.
For postpaid, a player is **required** (debt must attach to a person); the un-billed *guest* mode
remains the only no-account path.

### 5.4 Unified checkout

**Link POS to the session tab.** Add nullable `SessionId` to `PosSaleEntity` and to
`CreatePosSaleRequest`. Sales rung up while a session is active are attached to that session and
default to an **unpaid/open** state (tab), instead of being settled immediately. Standalone POS
sales (no session) keep today's immediate-payment behaviour.

**Checkout in one action.** New `SessionCheckoutService` + endpoint
`POST /api/sessions/{sessionId}/checkout` that:

1. Computes the **time charge** (open-tab postpaid: via `ComputeCheckoutChargeAsync`; fixed/prepaid:
   the already-known amount).
2. Gathers **attached unpaid POS sales** for the session.
3. Produces a **single grand total** = time charge + POS lines (same currency; reuse the existing
   per-player ledger currency guard).
4. Accepts **split payment**:

   ```csharp
   public sealed record SessionCheckoutRequest(
       Guid OrganizationId,
       IReadOnlyList<PaymentPartDto> Payments,   // one or more parts
       string IdempotencyKey);

   public sealed record PaymentPartDto(string PaymentMethod, MoneyDto Amount);
   ```

   - Extend `PaymentMethodNames` with `Wallet` (currently only `cash`, `card_manual`).
   - Validate `sum(Payments) == grandTotal`; a `Wallet` part is capped by the player's balance and
     deducts from the wallet ledger; cash/card parts are recorded as manual payments.
5. Writes the time-charge ledger entry (open-tab), marks attached POS sales **paid**, decrements
   stock as today, and records the payments — all in **one transaction**, idempotent on
   `IdempotencyKey`.
6. Ends the session (`State → ending/ended`) and dispatches the **lock** command to the device.
7. Produces a **receipt** (numbered, via the existing `ReceiptNumberGenerator`) covering time + POS
   as one document.

The operator UI surfaces this as **"Завершить и принять оплату"**: it shows "Наиграно Xч Yм • время Z •
снеки W • Итого N", a payment-method selector that supports multiple parts, then settles and locks.

**Members with a wallet:** the wallet part can be auto-suggested to cover the total; the operator
confirms. (Full member self-checkout is Track 3.)

### 5.5 Auto-protection

**Credit limit (D5).** Add `PostpaidCreditLimitMinorUnits` (nullable) to `BranchEntity` (per-branch
default) and an optional `PostpaidCreditLimitMinorUnits` (nullable) override on the player account.
Effective limit = player override ?? branch default ?? none. "None" means unbounded (logged as a
risk in the operator UI so it's a conscious choice).

**Hosted service** `AutoProtectionService` ticks (aligned to the heartbeat cadence) over active
sessions and, per session:
- **Fixed mode:** at `EndsAtUtc − 5 min`, push a **warning overlay** to the PC via the shell; at
  `EndsAtUtc`, dispatch **lock** and move the session to a "time-up, awaiting checkout" state
  (operator still settles the bill via §5.4).
- **Open-tab postpaid:** compute live accrued cost + attached unpaid POS; when it reaches the
  effective credit limit, push a **warning**, then **lock** and flag the seat for the operator. The
  session is not ended — the operator decides (extend the limit, settle, or close).

Warnings reuse the player-shell warning channel; locks reuse the existing device-command path.
Both are idempotent (don't re-warn/re-lock a session already in that state).

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `StartGuestSessionRequest` | `DurationMode` (open/fixed); `DurationMinutes` → nullable |
| `SessionDurationModes` | new constants (`Open`, `Fixed`) |
| `SessionEntity` | none for open-tab (`EndsAtUtc` already nullable); set `null` when open |
| `ISessionBillingService` | add `ComputeCheckoutChargeAsync`, `AppendCheckoutLedgerEntriesAsync` |
| `TariffBilling` | new pure `ComputeAmount(elapsed, tariffVersion)` (extracted from `EfTariffService`) |
| floor-map / `SessionDto` | add `accruedCostMinorUnits`, `currencyCode` on active session |
| `PosSaleEntity`, `CreatePosSaleRequest` | add nullable `SessionId`; tab sales default unpaid |
| `PaymentMethodNames` | add `Wallet` |
| `SessionCheckoutRequest`, `PaymentPartDto` | new contracts (split payment) |
| `BranchEntity` | add `PostpaidCreditLimitMinorUnits` (nullable) |
| `PlayerAccountEntity` | add `PostpaidCreditLimitMinorUnits` (nullable, override) |
| New endpoint | `POST /api/sessions/{sessionId}/checkout` |
| New endpoint (if absent) | `POST /api/branches/{branchId}/players` (create-in-flow) |
| New hosted service | `AutoProtectionService` |

Each change carries an EF migration; money stays `long` minor units end-to-end with conversion only
at the UI boundary (the existing, verified-correct convention).

## 7. Error Handling & Edge Cases

- **Idempotency** on start, checkout, and POS sale via explicit keys (existing pattern).
- **Concurrent checkout / double-tap:** idempotency key collapses retries; a second distinct
  checkout on an already-settled session returns the original result.
- **Currency mismatch:** reuse the per-player ledger currency guard; reject mixed-currency tabs.
- **Wallet part exceeds balance:** reject with a clear, recoverable error; operator re-splits.
- **Open-tab with zero billable time:** checkout produces a zero time charge but still settles any
  POS lines and locks the PC.
- **Force-end without checkout** (edge/maintenance): allowed for staff with permission; leaves the
  bill unsettled as debt (visible), never silently dropped.
- **Credit limit "none":** session runs unbounded; the seat tile shows a "no limit" marker.
- **Offline at checkout:** out of scope here; the Tier-0 reliability spec covers buffering. This
  spec assumes backend reachability at start and checkout (the current assumption).

## 8. Testing Strategy

- **Pure billing function:** unit tests for `TariffBilling.ComputeAmount` (min billable, rounding
  increments, overflow) — extends the existing tariff tests; the live display and checkout charge
  are asserted to use the same function and agree.
- **Open-tab lifecycle:** start (no duration) → accrue → checkout writes exactly one time-charge
  ledger entry of the expected amount; no start charge written.
- **Unified checkout:** time + N attached POS sales sum to one grand total; split payment parts must
  sum to the total; wallet part capped by balance; POS sales transition to paid; stock decremented;
  one receipt; lock dispatched; whole thing idempotent and transactional (all-or-nothing).
- **Auto-protection:** fixed session warns at −5 min and locks at end; open-tab postpaid warns and
  locks at the effective credit limit; no duplicate warns/locks; effective-limit resolution
  (player override > branch default > none).
- **Pickers/quick-account:** player search returns matches; create-in-flow yields a usable account;
  postpaid rejects start without a player.
- **Web/desktop parity:** the same start→checkout flow passes against both operator surfaces.

## 9. Decomposition & Sequencing

This spec is one coherent feature (the counter loop) but has five separable units. Suggested build
order for the implementation plan:

1. Open-tab model + pure billing function + deferred checkout charge (foundation).
2. Live accrued cost on the floor map (depends on 1).
3. Quick-account & pickers (independent UI/API; can parallel 2).
4. Unified checkout incl. POS `SessionId` link, `Wallet` method, split payment, receipt (depends on 1).
5. Auto-protection hosted service + credit-limit fields (depends on 1 & 2).

## 10. Future (v2 / other tracks)

- Member self-checkout and self-start from the PC shell (Track 3).
- Pause/transfer-preserving-tab, group/party tabs, reservations (operator polish).
- Tip/discount lines and permission-gated comps on the checkout (ties to the Tier-0 anti-fraud spec).
- Offline-buffered checkout (Tier-0 reliability spec).
