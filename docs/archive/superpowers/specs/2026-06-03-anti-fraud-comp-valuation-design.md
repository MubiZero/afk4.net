# Anti-fraud §5.4 — comp valuation, gate & visibility

**Date:** 2026-06-03 · **Branch:** `sp4-tier-0` · **Closes:** the deferred half of anti-fraud §5.4
(spec `2026-06-01-platform-anti-fraud-controls-design.md`).

## 1. Problem

§5.4 today (commit `b5676a0`, "variant A") only gates the *start* of a comp (free) session: it
checks the `IsComp` flag, requires a reason ≥ 8 chars, and writes a `session.comp` audit. But:

- the comp is **never valued** — a "free hour" shows up as a bare count, not "Alice comped 30 TJS";
- nothing is **persisted** on the session to mark it a comp;
- there is **no preventive control** — an operator can give away arbitrarily expensive free time;
- the §5.6 owner summary counts comps but cannot show their money value.

The deferred half of §5.4 is: **value the comp, gate it against the comp threshold, and surface the
value** everywhere comps already appear.

## 2. Decisions (locked with the user)

- **Scope** = valuation + visibility + a *simple synchronous gate*. No new async approval entity
  (we do **not** build a "pending comp request" flow; that stays a possible future extension).
- **A comp is fixed-duration with a real tariff.** `IsComp` now requires `DurationMode = Fixed`,
  `DurationMinutes > 0`, and a real `TariffVersionId`. Open-tab comps are rejected. This makes the
  comp value `duration × tariff` **known at start**, so the gate is always preventive and the
  checkout path needs no valuation logic.
- A comp **never bills**, regardless of what tariff id is stored on the session.

## 3. Data model

| Entity / contract | Change |
|---|---|
| `SessionEntity` | + `IsComp` (bool, default `false`); + `CompValueMinorUnits` (`long?`, null for non-comp). Migration `AddSessionComp`. |
| `SessionCommandResponse` | + `CompValueMinorUnits` (`long? = null`, trailing optional) so the start endpoint can audit the value and clients can display it. |
| `OwnerDailySummaryActorRowDto` / result DTO | + per-actor `CompValueMinorUnits` total (keep `CompCount`). |

No `LedgerEntry` is written for a comp — a comp is free; its value lives in the `session.comp` audit
amount and on the session row.

## 4. Behaviour

### 4.1 Start (`EfSessionCommandService.StartGuestSessionAsync`)

Extend the existing `if (request.IsComp)` block:

1. `billingMode` must be empty *(existing)*.
2. `CompReason.Trim().Length >= 8` *(existing)*.
3. **`DurationMode = Fixed` and `DurationMinutes > 0`** *(new — reject open-tab comp)*.
4. **`TariffVersionId` is required** *(new — needed to value)*.
5. Compute `compValue = tariffService.CalculateAsync(org, TariffVersionId, DurationMinutes).Amount`
   (reuses the existing billing calculator — minimum-billable + rounding identical to a real charge).
6. Resolve the comp threshold:
   `compThreshold = MoneyControlPolicy.ResolveCompThreshold(branch.CompApprovalThresholdMinorUnits,
   MoneyControlPolicy.ResolveApprovalThreshold(branch.HighRiskApprovalThresholdMinorUnits, default))`.
7. **Gate:** if `compValue > compThreshold` **and** the actor lacks comp-approval authority →
   reject (`Invalid`, no session/lease/command created): *"Comp value {X} exceeds the {Y} approval
   threshold; manager approval required."* A manager doing the comp themselves passes the gate.
8. On success: set `session.IsComp = true`, `session.CompValueMinorUnits = compValue`, and carry
   `compValue` into `SessionCommandResponse.CompValueMinorUnits`.

**Authority** is supplied by the endpoint: `StartGuestSessionAsync` gains a trailing
`bool actorCanApproveComp = false`; the endpoint passes
`StaffContext.Permissions` contains `ApproveMoneyAction`. (Keeps `StaffContext` out of the service.)

### 4.2 Checkout safety (`SessionBillingService.ComputeCheckoutChargeAsync`)

Guard at the top: **if `session.IsComp` → return a zero charge** (succeeded, amount 0). A comp never
bills, whatever tariff id sits on the row. This is the only checkout-side change (one guard); the
heavy "value-but-don't-charge at checkout" path is explicitly avoided.

### 4.3 Visibility

- **Audit:** the start endpoint already writes `session.comp`; set its `AmountMinorUnits = compValue`
  (init-prop) and include the value in `DetailsJson`.
- **§5.6 owner summary** (`OwnerDailySummaryAggregator`): comps become **valued** — sum
  `comp.AmountMinorUnits` into a per-actor `CompValueMinorUnits` (keep `CompCount`); include it in the
  row money-weight used for sorting. Add a `compValueTotal` token to the daily digest alongside
  `compCount`, rendered in en/ru/tg templates.

### 4.4 Daily-cap counting (`EfMoneyActionPolicyResolver.GetSpentTodayAsync`)

After the existing ledger high-risk sum, add today's comp values for the actor in the current shift:
sum `AuditRecords` where `Action = session.comp`, `ActorStaffUserId = actor`, `AmountMinorUnits` set,
and `CreatedAtUtc >= openShift.OpenedAtUtc`. So comps count toward the operator's daily high-risk
spend and subsequent refunds/corrections see them — closing the "launder a giveaway as a comp to free
up daily cap" gap. (Audits carry no `ShiftId`, so the window is bounded by the open shift's
opened-at timestamp.)

## 5. Tests (TDD)

- **Service:** under-threshold comp → starts, `IsComp`/`CompValueMinorUnits` set; over-threshold +
  no authority → reject (no session); over-threshold + authority → starts; open-tab comp → reject;
  comp without `TariffVersionId` → reject.
- **Checkout:** comp session → zero time charge.
- **Endpoint:** `session.comp` audit carries `AmountMinorUnits`.
- **Aggregator:** comps valued (sums amounts into the actor row).
- **Resolver:** a comp value counts in `GetSpentTodayAsync`.
- **Digest:** `compValueTotal` token renders.
- **Update** the existing open-tab comp happy-path test (`StartGuestSessionAsync_CompWithValidReason
  _StartsSession`) to fixed-duration + a real tariff.

## 6. Out of scope (honest deferrals)

- Async "pending comp request" approval flow (over-threshold comps are *blocked* unless the actor
  has authority, not queued).
- Open-tab comps (disallowed by decision; a comp grants a defined amount of free time).
- The silent-zero manual/guest path (`billingMode = ""` without `IsComp`) stays a non-comp start —
  unchanged from variant A; it is not forced through comp control.
