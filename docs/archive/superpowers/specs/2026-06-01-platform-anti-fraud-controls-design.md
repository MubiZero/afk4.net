# Anti-Fraud & Staff Accountability — Preventive Controls for Money Movements (Track: Trust)

- **Date:** 2026-06-01
- **Status:** Design (decisions proposed; pending founder review)
- **Scope owner:** Platform (AFK4)
- **Related:** [[platform-web-redesign]], [Counter Loop](2026-06-01-platform-counter-loop-postpaid-checkout-design.md) (owns guest mode), Notifications Backbone (Track 2, owns the owner-summary email)

## 1. Context & Problem

The product sells **postpaid play + cash handled by hired staff**. The owner is frequently
absent; the people touching money are employees. The single biggest commercial risk is
**staff theft** — a dishonest cashier or supervisor skimming cash, comping friends, or
quietly reversing real charges into their own pocket.

The accountability **foundations are strong** and must be preserved:

- **Every money movement is attributed.** `LedgerEntryEntity` records `CreatedByStaffUserId`,
  `ShiftId`, `CreatedAtUtc`, and a `Reason` on every entry
  (`src/AFK4.Platform.Api/Data/LedgerEntryEntity.cs`). Wallet top-ups, debt payments, manual
  corrections and refunds all flow through `EfBillingCommandService` and require an **open shift**
  (`RequireOpenShiftAsync`), so each entry is tied to a named operator on a named shift.
- **The ledger is append-only / immutable.** Nothing updates or deletes a `LedgerEntryEntity`;
  corrections and refunds are *new* entries. A refund writes a reversal entry carrying
  `ReversesLedgerEntryId` pointing at the original, and refunds are capped at the remaining
  refundable amount via `GetAlreadyRefundedAmountAsync`
  (`EfBillingCommandService.RefundLedgerEntryAsync`, lines 175–299). This is correct and stays.

But the controls are **detective, not preventive** — they tell you who did it *after* the money
is gone. The concrete gaps:

- **No caps, no second pair of eyes.** `RefundLedgerEntryAsync`, `ManualCorrectionAsync`
  (only an 8-char reason required, lines 332–335), and `PayDebtAsync` execute **immediately** for
  anyone holding the binary permission. `PermissionCatalog.cs` grants
  `RefundLedgerEntry`, `ManualLedgerCorrection`, and `PayDebt` to **ShiftSupervisor**,
  **BranchManager**, and **Owner** alike. A lone supervisor can refund or "correct" an unbounded
  amount, alone, at 3am, with the manager asleep.
- **Free-session hole.** `SessionBillingService.ValidateAsync` (lines 116–126) treats a
  null/empty `billingMode` as a *valid* session with `AmountMinorUnits = 0`. Staff can start
  un-billed sessions for friends; the only trace is a session row with no charge, visible only if
  someone reactively audits. There is no required reason and no manager flag.
- **Cash reconciliation is recorded but not enforced.** `EfShiftService.CloseShiftAsync`
  (lines 256–362) computes `expectedCash` from starting cash + cash movements + POS cash payments
  + cash ledger deltas, stores `DifferenceMinorUnits`, and lets the shift close **regardless of
  the size of the discrepancy**. A shortfall can be closed over and then papered with a
  `ManualCorrection`. No manager sign-off is forced on a large gap.
- **The owner can't review by actor.** `EfAuditSearchService` filters by action/outcome/target/
  date but **has no `ActorStaffUserId` filter** (`AuditSearchQuery.cs` has no actor field). The
  operator-actions report (`IReportService.GetOperatorActionReportAsync`) takes only
  `ReportSearchQuery(FromUtc, ToUtc, Limit)` — **no actor, no amount**. The owner literally cannot
  ask "show me every refund Alice did this week over 50 TJS."

This design adds the **preventive layer**: dual-control approval and caps on high-risk actions,
closes the free-session hole, forces sign-off on large cash discrepancies, and gives the owner
actor- and amount-filtered review plus a daily summary — **without breaking ledger immutability**.
Approvals are recorded as additional audit facts (`ApprovedByStaffUserId` + reason); originals are
never edited.

## 2. Goals

1. **Dual-control / manager approval** for high-risk money actions (refund, manual correction,
   debt write-off) whose amount exceeds a configurable threshold.
2. **Per-role daily and per-transaction caps** on those same actions.
3. **Close the free-session hole**: an un-billed session requires an explicit reason + a manager
   comp flag and is routed to a dedicated audit category for review (coordinating with the
   counter-loop spec, which owns guest mode).
4. **Actor-filtered and amount-filtered audit search** plus a **manager review screen** of pending
   approvals and recent high-risk actions.
5. **A daily owner summary** of refunds / comps / discrepancies grouped by actor (delivery rides
   on the Notifications Backbone spec).
6. **Shift close requiring manager sign-off** when the cash discrepancy exceeds a threshold.
7. **Optional refund-reason whitelist** to stop "misc" as a catch-all cover.

### Non-goals (deferred / owned elsewhere)

- The guest/comp **session mechanics** themselves — start flow, lock, un-billed lifecycle — are
  owned by the [counter-loop spec](2026-06-01-platform-counter-loop-postpaid-checkout-design.md).
  This spec only adds the *control* (reason + comp flag + audit category) on top.
- **Email/SMS delivery** of the owner summary and approval notifications — owned by the
  Notifications Backbone spec (Track 2). This spec defines the *content and trigger*; that spec
  carries it over the wire.
- Biometric/PIN re-auth at the till, CCTV integration, anomaly ML/scoring — future.
- Changing the append-only ledger model, or introducing editable entries — explicitly forbidden.

## 3. Proposed Decisions

These are the founder-facing forks. Defaults are best-practice starting points; flip any in review.

| # | Decision | Proposed default | Fork to weigh |
|---|----------|------------------|---------------|
| D1 | **Approval mode** for over-threshold high-risk actions | **Synchronous / blocking**: the action is created in a `PendingApproval` state and does **not** hit the ledger until a second authorised user approves. | Post-hoc review (act now, manager reviews later) is lower-friction but leaves a theft window. Recommend blocking for refunds/corrections/write-offs; it matches the "second pair of eyes" intent. |
| D2 | **Who can approve** | A user with the new `ApproveMoneyAction` permission whose `StaffUserId` **differs from the requester** (no self-approval). Granted to **BranchManager** and **Owner** only. | Allow ShiftSupervisor to approve each other? Rejected by default — supervisors are the main fraud vector here. |
| D3 | **Default approval threshold** (refund / manual correction / debt write-off) | **5000 minor units (50.00 TJS)** per transaction, configurable per branch. Below it: execute immediately but still audited and counted toward the daily cap. | Founder may want 0 (every refund needs approval) for a brand-new untrusted shop, or higher for a high-volume branch. Per-branch setting makes this tunable without code. |
| D4 | **Who gets caps** | **ShiftSupervisor and CashierOperator** get daily + per-transaction caps. **BranchManager and Owner are uncapped** (they are the approvers/escalation). | Founder may want managers capped too in multi-branch chains; structure supports it (caps are per-role config). |
| D5 | **Default daily cap** (sum of refunds + corrections + write-offs per actor per shift-day) | **ShiftSupervisor 20000 (200 TJS), CashierOperator 0** (cashiers cannot refund/correct at all — they already lack the permission today). | Founder may grant cashiers a small refund cap for convenience; default keeps them out. |
| D6 | **Shift-close discrepancy sign-off threshold** | **Manager sign-off required when `|Difference| > 2000` (20.00 TJS)**, configurable per branch. Within tolerance: close normally. | Could be a percentage of expected cash instead of absolute; absolute is simpler and predictable for small shops. |
| D7 | **Free/comp session control** | An un-billed session requires `Reason` (min 8 chars) **and** an explicit `IsComp` flag; if comp value (accrued time at standard tariff) exceeds D3 threshold, it needs the same approval as a refund. Routed to audit category `session.comp`. | Founder may want *all* comps to need approval regardless of value. Per-branch "comp threshold" reuses D3. |
| D8 | **Refund-reason whitelist** | **Off by default**; when enabled per branch, refunds must pick a reason from a configured list (free-text "Other" still allowed but flagged). | Whitelists reduce cover stories but add friction; opt-in per branch. |

## 4. Architecture Overview

The control layer sits **in front of** the existing immutable-ledger writes in
`EfBillingCommandService`, not inside them. High-risk commands are routed through a new
`MoneyActionGuard` that decides: execute-now, count-against-cap, or hold-for-approval. Approval and
caps are new tables; the ledger is untouched except that an executed entry now carries an optional
`ApprovedByStaffUserId` and the originating `MoneyActionRequestId` in its audit detail.

```
Operator (WPF / React)            Platform.Api                              Stores
  refund/correct/writeoff ─▶ BillingController
                                 │
                                 ▼
                          MoneyActionGuard  ── checks ──▶ caps  (StaffMoneyCapEntity, config)
                                 │            ├──────────▶ threshold (branch settings)
                                 │            └──────────▶ whitelist (branch settings)
              ┌──────────────────┼───────────────────────────┐
        under threshold     over threshold (D1 blocking)   cap exceeded
              │                  │                              │
              ▼                  ▼                              ▼
   EfBillingCommandService   MoneyActionRequestEntity      reject (clear,
   (append ledger entry,      state=PendingApproval         recoverable error;
    audited as today)         (no ledger write yet)          ask a manager)
                                 │
                  manager approve (≠ requester) ──▶ MoneyActionGuard.Execute
                                 │                    ──▶ EfBillingCommandService (append entry,
                                 ▼                         entry.ApprovedByStaffUserId set,
                          audit: money_action.approved      audit: money_action.executed)

  shift close ─▶ EfShiftService.CloseShiftAsync
                   └─ if |Difference| > D6 ⇒ require ManagerSignOffStaffUserId (≠ closer)

  Reviewability:
   EfAuditSearchService + AuditSearchQuery   ── add ActorStaffUserId, MinAmount, MaxAmount
   ReportSearchQuery / operator-actions      ── add ActorStaffUserId, amount range
   Manager Review screen (Platform.Web)      ── pending approvals + high-risk feed
   DailyOwnerSummaryService (hosted)         ── refunds/comps/discrepancies by actor ─▶ Notifications
```

Six independently testable components:

1. **MoneyActionGuard** — pure-ish policy: threshold + cap + whitelist decision over a request.
2. **Approval workflow** — `MoneyActionRequestEntity`, request/approve/reject, no-self-approval.
3. **Per-role caps** — `StaffMoneyCapEntity` config + daily-spend aggregation.
4. **Free-session control** — reason + comp flag + `session.comp` audit routing (with counter-loop).
5. **Reviewability** — actor/amount filters on audit search and operator-actions report + Review UI.
6. **Daily owner summary** — hosted aggregation feeding the notifications backbone.
7. **Shift-close sign-off** — discrepancy threshold gate in `CloseShiftAsync`.

## 5. Components

### 5.1 MoneyActionGuard (policy core)

A new `MoneyActionGuard` evaluates every high-risk request **before** any ledger write. Inputs:
action type (`Refund` | `ManualCorrection` | `DebtWriteOff`), absolute amount in minor units,
actor's roles, branch policy (threshold, caps, whitelist), and the actor's spend-so-far today.

Output is one of:

- **`ExecuteNow`** — amount ≤ threshold *and* within the actor's remaining daily cap. Proceeds to
  the existing `EfBillingCommandService` path unchanged.
- **`RequireApproval`** — amount > threshold (D3) or comp value > comp threshold (D7). Creates a
  `MoneyActionRequestEntity` in `PendingApproval`; **no ledger entry yet**.
- **`Reject`** — would breach the actor's daily cap (D5) even with approval-less execution, or a
  whitelist violation when D8 is on. Returns a clear, recoverable error naming the cap.

`DebtWriteOff` is a *new* logical action distinguishing "customer paid the debt" (`PayDebt`, cash
actually collected) from "we forgive the debt" (a manual correction that erases debt with **no cash
in**). Today both can be done via `ManualCorrection`/`PayDebt`; the write-off path (debt-reducing
correction with no matching cash) is the higher-risk one and is the one that needs approval. The
guard classifies a `ManualCorrection` that reduces a `Debt` account as a write-off for policy.

The guard is deterministic and unit-tested in isolation (no DB): given (amount, threshold, cap,
spent, roles) it returns the decision.

### 5.2 Approval workflow

**New entity `MoneyActionRequestEntity`** (append-only, like the ledger — state transitions are
recorded as new audit facts, the row's terminal state is set once):

| Field | Notes |
|---|---|
| `MoneyActionRequestId` (PK) | |
| `OrganizationId`, `BranchId`, `ShiftId` | scope |
| `ActionType` | `refund` / `manual_correction` / `debt_write_off` |
| `RequestedByStaffUserId` | the originator |
| `PayloadJson` | the original command request (amount, currency, reason, target ledger entry / player) so approval can replay it verbatim |
| `AmountMinorUnits`, `CurrencyCode` | denormalised for cap/threshold reporting |
| `Reason` | requester's reason (≥ 8 chars, or whitelist value) |
| `State` | `pending` → `approved` / `rejected` / `expired` |
| `ApprovedByStaffUserId` (nullable) | the second pair of eyes; **must differ** from requester |
| `DecisionReason` (nullable) | approver/rejecter note |
| `CreatedAtUtc`, `DecidedAtUtc`, `ExpiresAtUtc` | pending requests expire (default 24h) |
| `ResultingLedgerEntryId` (nullable) | set when approved & executed — links request → ledger |

**Endpoints** (sketch):

- `POST /api/branches/{branchId}/money-actions` — submit a high-risk action. If the guard says
  `ExecuteNow`, this *is* today's refund/correction call and returns the ledger entry. If
  `RequireApproval`, returns `202` with a `MoneyActionRequestId` in `pending`.
- `POST /api/branches/{branchId}/money-actions/{id}/approve` — requires `ApproveMoneyAction`;
  rejects self-approval (`approver == requester` ⇒ 403). On success, **runs the original command
  through `EfBillingCommandService`** (so the ledger write, idempotency, currency guard, refund-cap
  checks are all the existing, verified code), stamps the new entry's audit detail with
  `ApprovedByStaffUserId` and `MoneyActionRequestId`, sets the request `approved` +
  `ResultingLedgerEntryId`.
- `POST /api/branches/{branchId}/money-actions/{id}/reject` — `ApproveMoneyAction`; sets `rejected`
  + reason; no ledger effect.
- `GET /api/branches/{branchId}/money-actions?state=pending` — feeds the Review screen.

**Ledger immutability preserved:** approval never edits the original entry. A refund is still a new
reversal entry with `ReversesLedgerEntryId`; the only addition is that its creation was gated and
its audit record now also names the approver. The `ApprovedByStaffUserId` lives on the audit record
/ request, optionally mirrored to a nullable `ApprovedByStaffUserId` on `LedgerEntryEntity` for
direct queryability (see §6).

**Idempotency:** the original `IdempotencyKey` travels in `PayloadJson`; executing on approval
reuses `EfBillingCommandService`'s existing idempotency, so a double-approve collapses to one entry.

### 5.3 Per-role caps

**New config `StaffMoneyCapEntity`** keyed by `(BranchId, RoleName, ActionScope)` with
`PerTransactionMinorUnits` and `DailyMinorUnits` (null = unlimited). Seeded from D4/D5 defaults;
editable by Owner/BranchManager via branch settings (a new `ManageMoneyControls` permission, or
fold into existing `ManageBranchSettings` — **Proposed: reuse `ManageBranchSettings`** to avoid
permission sprawl).

**Daily spend** = sum of `|AmountMinorUnits|` for that actor's refund + manual-correction +
write-off ledger entries within the current shift-day (UTC day or shift window — **Proposed:
shift-day = the actor's open shift**, since money is already shift-scoped and shifts are the natural
reconciliation unit). The guard queries this aggregate (cheap, indexed on
`(CreatedByStaffUserId, CreatedAtUtc, EntryType)`) before deciding.

### 5.4 Free-session control (with counter-loop)

The counter-loop spec owns the un-billed *guest* mode. This spec layers the control:

- A session started with no billing mode is **no longer silently zero-cost**. The start path
  requires an explicit `comp` intent: `Reason` (≥ 8 chars) + `IsComp = true`. Replaces the silent
  `AmountMinorUnits = 0` branch in `SessionBillingService.ValidateAsync` (lines 116–126) — a
  missing billing mode **without** the comp intent becomes an error ("billing mode required").
- The comp is **valued**: at end/checkout, compute the would-be charge at the standard tariff. That
  value is what counts toward the comp threshold (D7) and the daily cap, and what the owner summary
  reports — so a "free hour" shows up as "Alice comped 30 TJS", not as nothing.
- Comps route to a dedicated audit action `session.comp` (new `AuditActionNames` constant) so the
  Review screen and owner summary can surface them as a first-class category, distinct from refunds.
- If comp value > comp threshold, it goes through the §5.2 approval flow (manager must approve the
  free session). Below threshold: allowed but counted and audited.

Coordination note: the counter-loop spec must call this control at the comp start/checkout boundary;
the field (`IsComp`, `Reason`) lives on the start request it already owns.

### 5.5 Reviewability — filters + Review screen

**Audit search.** Extend `AuditSearchQuery` with `ActorStaffUserId`, `MinAmountMinorUnits`,
`MaxAmountMinorUnits`; add the matching `Where` clauses in `EfAuditSearchService.SearchAsync`. Amount
filtering requires amounts to be queryable — see §6 (denormalise amount onto the audit record, or
filter via a join to ledger entries for money actions). **Proposed: store `AmountMinorUnits`
(nullable) on the audit record** for money-relevant actions so the filter is a simple indexed range.

**Operator-actions report.** Extend `ReportSearchQuery` with `ActorStaffUserId` and an amount range,
and thread it through `GetOperatorActionReportAsync` / `EfReportService` / `ReportCsvExporter`. This
directly answers "all refunds by Alice this week over 50 TJS" as a filtered report + CSV export.

**Manager Review screen** (Platform.Web, manager/owner): two panels —
(a) **Pending approvals** (the `state=pending` feed) with one-tap approve/reject; and
(b) **Recent high-risk actions** (refunds, corrections, write-offs, comps, large discrepancies)
filterable by actor, action, amount, date — backed by the extended audit search. This is the
owner's daily "watch the staff" surface.

### 5.6 Daily owner summary

**New hosted `DailyOwnerSummaryService`** runs once per branch per day (aligned to shift-day close),
aggregating, **grouped by actor**: total refunds, total comps (valued), total manual corrections /
write-offs, and shift discrepancies. It produces a structured summary payload and hands it to the
**Notifications Backbone** (Track 2) for email delivery to the owner. This spec defines the payload
shape and trigger; the backbone owns transport, templating, retry, and opt-out. Until the backbone
lands, the same summary is available on demand via a report endpoint (degrade gracefully — the data
exists regardless of delivery channel).

### 5.7 Shift-close sign-off

In `EfShiftService.CloseShiftAsync`, after computing `difference` (line 338): if
`Math.Abs(difference) > branchDiscrepancyThreshold` (D6) **and** no `ManagerSignOffStaffUserId` is
supplied on the request, **reject the close** with a recoverable error ("discrepancy of X exceeds
tolerance; manager sign-off required"). The cashier then fetches a manager, who re-submits the close
with their `StaffUserId` (which must hold `CloseShift`/`ManageShiftCash` **and differ from** the
opening/closing operator — no self-sign-off). Persist `ManagerSignOffStaffUserId` and
`SignOffReason` on the shift; emit an audit fact. Within tolerance, close behaves exactly as today.

This stops the "close over a shortfall, then quietly `ManualCorrection` it away" path: the shortfall
is now sign-off-gated *and* the correction that would hide it is itself approval-gated (§5.2).

## 6. Data Model Changes (summary)

| Entity / contract | Change |
|---|---|
| `MoneyActionRequestEntity` | **new** — approval workflow record (§5.2); append-only, terminal state set once |
| `StaffMoneyCapEntity` | **new** — per-(branch, role, action) per-transaction + daily caps |
| `BranchEntity` / branch settings | add `HighRiskApprovalThresholdMinorUnits`, `CompApprovalThresholdMinorUnits`, `ShiftDiscrepancyToleranceMinorUnits`, `RefundReasonWhitelistJson` (nullable), `RefundReasonWhitelistEnabled` |
| `LedgerEntryEntity` | add nullable `ApprovedByStaffUserId` and nullable `MoneyActionRequestId` (queryability + provenance; original fields untouched) |
| `ShiftEntity` | add nullable `ManagerSignOffStaffUserId`, `SignOffReason` |
| Audit record | add nullable `AmountMinorUnits` (for amount-range filtering of money actions) |
| `AuditActionNames` | add `session.comp`, `money_action.requested/approved/rejected/executed`, `shift.signoff` |
| `AuditSearchQuery` | add `ActorStaffUserId`, `MinAmountMinorUnits`, `MaxAmountMinorUnits` |
| `ReportSearchQuery` | add `ActorStaffUserId`, `MinAmountMinorUnits`, `MaxAmountMinorUnits` |
| `RefundLedgerEntryRequest`, `ManualLedgerCorrectionRequest` | optional `ReasonCode` (whitelist) |
| `CloseShiftRequest` | optional `ManagerSignOffStaffUserId`, `SignOffReason` |
| `StaffPermissionNames` / `PermissionCatalog` | add `ApproveMoneyAction` (BranchManager, Owner); **remove `RefundLedgerEntry`/`ManualLedgerCorrection` from ShiftSupervisor?** — **Proposed: keep them, but gate by approval** rather than revoke, so supervisors still operate under managers |
| New endpoints | `POST/GET …/money-actions`, `…/money-actions/{id}/approve`, `…/{id}/reject`; daily-summary report endpoint |
| New hosted service | `DailyOwnerSummaryService` |

Money stays `long` minor units end-to-end; conversion to major units only at the UI boundary (the
existing, verified convention — `formatCurrency` takes major units). Each change carries an EF
migration. **The ledger remains append-only**; no entry is ever updated or deleted.

## 7. Error Handling & Edge Cases

- **Self-approval** — `approver == requester` ⇒ 403, even with the permission. No exceptions.
- **Approver lacks permission** — 403; the request stays `pending`.
- **Pending request expires** (default 24h) — auto-`expired`; no ledger effect; surfaced in the
  Review screen so it isn't silently lost. Operator must resubmit.
- **Cap breach with no manager available** — `Reject` with a clear message; the action simply
  cannot happen until a manager approves or the next shift-day resets the daily counter. This is
  the intended friction.
- **Double-approve / retry** — original `IdempotencyKey` in the payload makes execution idempotent;
  the second approve returns the original ledger entry, not a duplicate.
- **Refund still bounded** — the existing remaining-refundable check
  (`GetAlreadyRefundedAmountAsync`) runs at execution time, so an approval granted before another
  partial refund can still be rejected at execution if it would over-refund (fail safe).
- **Currency mismatch** — existing per-player ledger currency guard
  (`GetLedgerCurrencyForWriteAsync`) and the refund currency check run unchanged at execution.
- **Comp valuation when tariff missing** — if no standard tariff exists to value a comp, treat the
  comp as **always requiring approval** (can't bound the risk ⇒ escalate).
- **Shift-close within tolerance** — unchanged path; no sign-off required, no extra friction.
- **Manager signs off own shift** — rejected (`signoff == closer`/opener) where a second user exists;
  single-staff branches are a **Proposed** config exception (tiny shops with one trusted owner-
  operator may disable sign-off entirely per branch).
- **Whitelist off** — refund reason behaves exactly as today (free text, ≥ 1 char today; recommend
  raising refund reason to ≥ 8 chars to match manual corrections — **Proposed**).

## 8. Testing Strategy

- **MoneyActionGuard (pure):** table-driven tests over (amount, threshold, cap, spent, roles) ⇒
  `ExecuteNow` / `RequireApproval` / `Reject`; boundary at exactly the threshold and exactly the
  cap; write-off classification of debt-reducing corrections.
- **Approval workflow:** request over threshold creates `pending` with no ledger entry; approve by a
  different user writes exactly one ledger entry carrying `ApprovedByStaffUserId` +
  `ResultingLedgerEntryId`; self-approve is forbidden; reject leaves the ledger untouched; expiry
  transitions to `expired`; double-approve is idempotent.
- **Caps:** sum-by-actor-by-shift-day aggregation correct; an action that would breach the daily cap
  is rejected; the next shift-day resets; managers/owners uncapped.
- **Free-session control:** missing billing mode without comp intent now errors; a comp requires
  reason + flag, is valued at standard tariff, routes to `session.comp`, and escalates to approval
  above the comp threshold. (Joint test with the counter-loop suite at the comp boundary.)
- **Reviewability:** audit search filters by actor and amount range; operator-actions report +
  CSV honour actor + amount; "refunds by Alice over 50 TJS this week" returns the expected set.
- **Shift-close sign-off:** discrepancy within tolerance closes as today; over tolerance without
  sign-off is rejected; with a *different* manager's sign-off it closes and records the sign-off;
  self-sign-off rejected.
- **Owner summary:** aggregation groups by actor with correct refund/comp/correction/discrepancy
  totals; payload handed to the notifications stub; on-demand report returns the same numbers.
- **Immutability regression:** assert no code path updates or deletes a `LedgerEntryEntity`; refunds
  remain reversal entries with `ReversesLedgerEntryId`.

## 9. Decomposition & Sequencing

Independent units; suggested build order for the implementation plan:

1. **MoneyActionGuard + branch policy fields + caps config** (foundation; pure-logic first).
2. **Approval workflow** (`MoneyActionRequestEntity`, endpoints, no-self-approval, execute-on-
   approve through existing `EfBillingCommandService`) — depends on 1.
3. **Reviewability** — audit/report actor+amount filters (independent; high owner value early) and
   the Manager Review screen (depends on 2 for the pending feed).
4. **Shift-close sign-off** (`CloseShiftAsync` gate) — independent of 1–3.
5. **Free-session control** — coordinated with the counter-loop spec at the comp boundary; depends
   on 1 for valuation/threshold.
6. **Daily owner summary** — depends on the audit/aggregation data; delivery depends on the
   Notifications Backbone (degrade to on-demand report until then).

## 10. Future (v2 / other tracks)

- PIN/biometric re-auth at the till for high-risk actions (a third factor on top of approval).
- Anomaly scoring: flag actors whose refund/comp rate deviates from peers, surfaced in the Review
  screen.
- Tie comps/discounts on the counter-loop checkout into the same approval pipeline (the counter-loop
  spec's "tip/discount lines" future item).
- Owner-configurable approval routing (e.g. escalate to a specific phone for SMS approval) once the
  notifications backbone supports inbound actions.
- CCTV/transaction time-correlation for investigations.
