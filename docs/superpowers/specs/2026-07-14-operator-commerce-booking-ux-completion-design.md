# Operator Commerce And Booking UX Completion Design

**Status:** approved for planning
**Date:** 2026-07-14
**Scope:** Operator App, POS settlement/refunds, and reservation-to-session start

## Goal

Finish the remaining Operator App commerce and booking remarks on top of the
consolidated Operator UI and the verified commerce-financial-integrity slice.
The result must preserve one authoritative money path, make common cashier
actions clear and fast, and turn a confirmed reservation into exactly one
backend-approved session.

## Context

The desired product state is split across two verified topic branches:

- `feat/operator-ui-consolidated` contains the current consolidated Operator
  UI on top of the latest verified `origin/main` UI baseline.
- `feat/commerce-financial-integrity-impl` contains the Player Shop to POS,
  wallet, receipt, shift, inventory-cost, and refund integrity work.

Several original remarks are already partly implemented. `PaymentDialog` is
shared by Map and POS, the Map checkout accepts split payments, booking has a
linked-client picker, and reservations have `pending`, `confirmed`, and
`seated` states. The remaining problem is not to rebuild those pieces. It is to
integrate them, close incomplete data paths, and make their behavior consistent.

The current POS UI is a concrete example of an incomplete path: it can render
payment parts, but submits only the first method as a full-total manual payment.
The current reservation `seat` command only changes reservation state and then
opens the seat on the Map; it does not start a session. Both cases require
backend completion, not frontend-only polish.

## Product Decisions

1. This epic does not implement online-reservation money holds, mobile booking
   tariff/package selection, or automatic no-show processing. Those remain a
   separate money-path epic described in
   `2026-06-18-online-booking-autoconfirm-hold.md`.
2. The cash journal remains. `Смена` is the aggregate operational summary;
   `Журнал кассы` is the transaction-level home for cash operations, receipts,
   refunds, and anti-fraud approvals. Removing it would remove capabilities,
   not merely duplicated navigation.
3. Current device/seat state does not prevent an operator from selecting a PC
   for a future group reservation. Actual reservation/session overlap for the
   selected time interval remains authoritative and blocks creation.
4. Pending reservations are explicitly confirmed before they can start.
   Confirmed reservations expose `Начать сессию`; a successful start produces
   one session and moves the reservation to `seated` atomically.
5. Manual money corrections retain the existing anti-fraud workflow. This epic
   adds regression coverage but does not weaken caps, approval requirements,
   immutable ledger behavior, or audit.
6. Inventory economics retain immutable sale-line cost/stock snapshots and
   weighted-average inbound reconciliation from the commerce integrity slice.

## Integration Strategy

Create a new topic branch from the verified `feat/operator-ui-consolidated`
tip. Apply only the commerce design, plan, and implementation commits whose
range begins after the old Operator UI source tip and ends at the verified
commerce implementation tip. Do not merge the commerce branch's full iterative
Operator UI ancestry.

This approach preserves the consolidated UI tree, keeps current `origin/main`
QA fixes, and imports the reviewed financial slice without replaying
superseded UI states. The exact immutable source/base commit IDs must be
recorded at execution time. Any conflict is resolved by product behavior and
then covered by a focused test.

Rejected approaches:

- merging the whole commerce branch into the consolidated UI branch, because
  it reintroduces a long divergent UI history and avoidable conflicts;
- independent unintegrated feature branches, because payment, refund, booking,
  and shared Operator primitives must be verified together before publication.

## Operator UI Behavior

### New Session Client Selection

The Map `Новая сессия` flow uses the same linked-client interaction model as
booking:

- typing at least two characters loads matching club clients;
- every result is a real button/option and can be selected with pointer or
  keyboard;
- selection stores `PlayerAccountId`, displays the chosen client, balance, and
  available packages;
- clearing selection removes all linked-client/package state;
- guest mode is an explicit choice and never looks like a failed client lookup;
- changing the query after selection unlinks the previous account before any
  session command can run.

The backend remains authoritative. A stale balance/package shown in the picker
does not authorize a session.

### POS Product, Cart, And Stock Health

- A product card renders `Цена:` immediately before the formatted retail
  amount.
- A selected client's cart identity renders `Баланс:` immediately before the
  formatted wallet balance.
- Both labels are localized through the existing `@afk4/i18n` catalogs.
- Branch stock-health counts include only products whose authoritative
  `TrackStock` value is true. Non-stock services such as a guest hour do not
  become low/out-of-stock solely because their quantity is zero.
- A tracked product at zero remains out of stock even when its reorder
  threshold is zero. A zero threshold disables only the low-stock warning above
  zero; it does not redefine zero stock as healthy.

### Order Cards

- Hover/focus may change border, surface, or shadow but must not translate the
  order card outside its stacking context.
- The `Подробнее` copy is removed because the whole card is the disclosure
  control.
- The chevron points right while collapsed and rotates down while expanded.
- Rotation uses the existing reduced-motion policy; the expanded state is also
  represented by `aria-expanded`, not animation alone.
- Expanding one card does not move its border beneath adjacent cards, clip its
  focus ring, or make nested actions trigger an unintended collapse.

## Unified POS Payment

### Dialog Behavior

Map session checkout and POS checkout continue to render the same
`PaymentDialog` and pure `checkoutState` validation.

POS passes the selected player's current wallet balance and enables split mode
only when the sale is linked to that player. The available parts are:

- `cash`;
- `card_manual`;
- `wallet` when a linked player is present.

Each method may appear at most once. The add-method control offers only unused
methods. Non-cash parts must not exceed the amount due. Wallet parts must not
exceed the displayed balance, while the backend repeats the authoritative
ledger check. Cash is tendered money: any excess becomes change and only the
applied cash amount is persisted.

The dialog confirms only when applied parts equal the sale total. It shows the
remaining amount, wallet limit, invalid input, non-cash overpayment, or cash
change inline without clearing the operator's entries.

### Backend Contract

Add a POS settlement request carrying:

- organization ID;
- a non-empty list of `PaymentPartDto`;
- note;
- idempotency key.

The list uses the existing shared session payment-part DTO instead of defining
a second transport shape. Validation requires:

- one to three parts;
- a supported and unique method per part;
- positive part amounts;
- one normalized currency matching the sale;
- an exact total equal to the sale total;
- `wallet` only when the sale has a linked player;
- wallet debit not exceeding the authoritative available balance.

### Settlement Transaction

POS owns the sale, payment rows, receipt, and stock movements. Billing owns the
wallet ledger. Inventory owns cost/stock rules. A scoped POS settlement
coordinator opens one serializable transaction and asks those module boundaries
to stage mutations in the shared unit of work.

For a valid draft or pending-payment sale it atomically:

1. Reloads the sale, open shift, lines, and immutable line snapshots.
2. Validates stock and every payment part.
3. Debits the linked wallet for the wallet part through Billing.
4. Creates one `PaymentEntity` per part.
5. Creates tracked stock movements from immutable line snapshots.
6. Creates one sale receipt for the total.
7. Marks the sale paid.
8. Commits and only then emits low-stock notifications.

Any failure rolls back wallet, payments, stock, receipt, and sale-state changes.
The unpaid sale may remain retryable, but no partial financial effect may
remain.

The command is idempotent by sale ID plus request idempotency key and normalized
payment parts. The same key and same payload returns the committed result. The
same key with different parts returns `idempotency_conflict`. Concurrent
attempts result in one paid sale; the loser replays the committed result or
returns a stable state/version conflict.

The existing single manual-payment endpoint remains a compatibility adapter
that delegates to settlement with one part. New Operator UI calls the multipart
contract.

## Refunds And Cash Journal

A refund is based on immutable original payment rows, not on the operator's
current payment selection:

- wallet parts create an equal Billing reversal for the linked player;
- cash and manual-card parts create corresponding refund payment rows;
- tracked products return to inventory using the immutable sale-line cost and
  stock-tracking snapshots;
- one refund receipt represents the full sale refund;
- the sale becomes `refunded` exactly once.

The refund transaction is atomic and idempotent. Repeated submission returns
the existing refunded projection without a second wallet credit, payment row,
receipt, or stock return. Linked Player Shop sales continue through the
commerce coordinator so order cancellation and sale refund remain one unit.
Ordinary POS sales use the same underlying refund boundary without a Shop
order mutation.

`Журнал кассы` retains three permission-gated segments:

- operations;
- receipts and receipt details/actions;
- anti-fraud review.

Refund actions live with receipt details. After success the selected receipt,
list row, shift aggregates, and stock projection refresh from backend state.
Removing navigation or duplicating refund actions inside `Смена` is outside
this epic.

## Booking Client And Multi-Seat Selection

### Client Picker

Booking keeps the existing controlled `ClientPicker`, with these requirements:

- pointer and keyboard selection are equivalent;
- selected results show name, phone, balance/debt, and a linked-account badge;
- free text remains a guest booking and never retains a stale
  `PlayerAccountId`;
- loading and empty states do not flicker during debounced search;
- clearing a linked client clears the derived phone and financial summary;
- failures keep the typed query and permit retry.

The New Session flow reuses this component or a shared extraction of its
behavior; it must not create a third client-search implementation.

### Ctrl Multi-Selection

On the booking timeline:

- Ctrl-click toggles any seat into or out of the draft selection;
- Command-click is equivalent on platforms that emit `metaKey`;
- selection may cross zones and need not be contiguous;
- current `occupied`, `offline`, `maintenance`, or other non-free visual state
  does not disable selection for a future interval;
- selected seats appear as removable chips and preview blocks;
- current device/seat state is shown as a warning, not silently discarded;
- an actual active reservation/session overlap at the chosen interval is a
  blocking conflict;
- group creation remains all-or-nothing on the backend.

Plain click keeps the existing single-seat/select behavior. Drag selection
continues to support contiguous multi-seat booking. Ctrl/meta selection is an
additional input method, not a replacement.

## Reservation Confirmation And Session Start

### State And UI Rules

- `pending`: operator may confirm or cancel; session start is unavailable.
- `confirmed`: operator may start, move, or cancel.
- `seated`: displays the linked active/created session and cannot start again.
- `cancelled`: no start or confirm action.

Starting opens the established session-start form prefilled with reservation
seat and linked client. The operator still chooses the valid billing mode,
tariff, and package required by the Sessions boundary. A reservation without a
linked player starts only as a guest unless the operator explicitly links a
client before submission.

### Contract And Coordinator

Add an idempotent reservation-session start command containing:

- organization ID;
- reservation ID from the route;
- the existing session-start billing selection fields;
- idempotency key;
- expected reservation version.

Add a monotonically increasing `Version` concurrency token to reservations and
return it in reservation projections. Every confirm, update, cancel, legacy
seat, and start-session command compares the expected version before mutation.

A reservation-session coordinator owns the cross-module transaction. It does
not let Reservations directly mutate Sessions data or Sessions directly mutate
Reservations data. It:

1. Loads the reservation in organization/branch scope.
2. Requires `confirmed`, a seat, `now < EndsAtUtc`, and the expected version.
3. Verifies that request player/guest identity matches the reservation choice.
4. Invokes the Sessions start boundary with the reservation seat and billing
   selection.
5. Stages the session, billing effects, session events, lease/device command
   intent, and reservation transition.
6. Sets `seated`, `SeatedAtUtc`, and the durable created session link.
7. Commits before dispatching realtime/device side effects.

The reservation stores the resulting session ID so replay can return the same
session. A migration adds that nullable link and its uniqueness constraint. A
confirmed reservation can link to at most one session, and a session can be
the start result of at most one reservation.

If the seat becomes unavailable, billing validation fails, or the transaction
cannot commit, the reservation remains confirmed and no session/billing effect
survives. Concurrent starts create exactly one session. The old `seat` endpoint
remains only as a compatibility path until all callers move; the Operator UI
uses the start-session command and never treats a state-only seat transition as
a started session.

Starting before `StartsAtUtc` is allowed because staff may seat an arriving
player early; the UI warns that billing begins immediately. Starting at or
after `EndsAtUtc` is rejected as `reservation_expired`.

## Stable Errors And Recovery

New or converged endpoints return stable machine-readable codes:

- `open_shift_required`;
- `invalid_payment_split`;
- `mixed_currency`;
- `insufficient_funds`;
- `player_required_for_wallet`;
- `out_of_stock`;
- `reservation_confirmation_required`;
- `reservation_already_started`;
- `reservation_expired`;
- `seat_unavailable`;
- `version_conflict`;
- `idempotency_key_required`;
- `idempotency_conflict`.

Operator error projection maps these codes to localized actionable messages.
On failure:

- POS keeps the cart, selected client, and payment draft;
- booking keeps client, seats, time, and billing choice;
- authoritative lists refresh after state/version conflicts;
- a network-ambiguous result first retries/replays with the same idempotency
  key;
- only an explicit new operator retry gesture creates a new key.

## Module Boundaries

- Reservations owns reservation state and the reservation-to-session link.
- Sessions owns session lifecycle, session events, leases, and device command
  intent.
- Billing owns immutable wallet/package/debt ledger effects.
- POS owns sale state, payment records, and receipts.
- Inventory owns stock availability, movements, and average cost.
- Shifts owns open-shift eligibility and reconciliation projections.
- Audit records the actor and outcome through its explicit writer.
- Cross-module coordinators own transaction orchestration only; they call
  narrow module services and do not duplicate domain rules.
- Operator Web sends commands and renders authoritative responses. Its cache is
  never financial or session authority.

## Testing Strategy

### Operator Web

- New Session client results support click, Enter, arrows, clear, guest, and
  stale-link removal.
- Product and cart labels are localized and visible.
- Stock-health tests distinguish tracked zero stock, low stock, healthy stock,
  and non-stock zero quantity.
- Order disclosure tests assert `aria-expanded`, removed copy, chevron state,
  nested-action behavior, and reduced motion.
- Payment tests cover each single method, every supported two/three-part split,
  duplicate methods, wallet limit, exact total, card overpayment, cash change,
  failure retention, and explicit retry.
- Booking tests cover linked/guest client behavior, Ctrl/meta toggle across
  zones and current seat states, interval conflicts, confirmation gating, and
  start-session success/failure.

### Shared Contracts And Backend

- Serialization tests lock multipart POS settlement and reservation-session
  start request/response shapes.
- POS service tests prove exact split validation, wallet ownership, atomic
  ledger/payment/receipt/stock state, idempotency, and symmetric refund.
- Reservation coordinator tests prove state gating, identity/billing
  propagation, session linkage, transaction rollback, audit, and replay.
- Existing manual-correction anti-fraud tests remain green and receive a
  regression case through the retained journal surface.
- Report/CSV tests prove split payments and refunds reconcile by method without
  changing immutable COGS calculations.

### PostgreSQL Concurrency

Deterministic overlapping live-PostgreSQL tests cover:

- two settlement requests for one POS sale;
- wallet balance contention during POS settlement;
- last tracked unit contention;
- two starts for one reservation;
- reservation start racing with another session taking the seat;
- refund replay/race after mixed payment.

Sequential tests do not satisfy these concurrency requirements.

### Final Verification

Before push:

1. Focused affected Operator Web and backend tests during each TDD task.
2. Full Operator Web test suite and production build.
3. Full Player Shell Web tests/build when integration changes shared web code.
4. Shared Contracts tests.
5. Platform API tests against an isolated PostgreSQL 16 database.
6. Full solution build with the required Windows-targeting setting when run in
   the Linux harness.
7. `git diff --check`, staged-diff review, and whole-branch review.
8. On Windows, run Operator/Player Shell WindowsDesktop testhosts and the
   affected Agent packaging checks before merge/release approval.

Required CI must be green on the latest published head before merge.

## Documentation

The implementation updates the compact progress snapshot only when the
integrated branch becomes the durable current state or exposes a new verified
gap. Detailed concurrency/verification evidence belongs in a focused report or
archive, not as a long progress log.

The final handoff reports:

- integration source/base commits and conflict decisions;
- implemented behavior by slice;
- test/build/database evidence;
- Windows-only verification status;
- branch, commit, push, PR, and merge status.

## Acceptance Criteria

- The integrated branch contains the consolidated Operator UI and verified
  commerce financial-integrity behavior without restoring superseded UI.
- New Session client results are clickable/keyboard-selectable and preserve a
  correct guest/linked-client distinction.
- POS shows `Цена:` and `Баланс:` labels and excludes non-stock services from
  stock-health alerts.
- Order cards expand without border clipping or translation and use an
  accessible rotating chevron without `Подробнее`.
- Map and POS use the same payment dialog and validation model.
- POS multipart settlement and refund are atomic, idempotent, wallet-aware,
  inventory-safe, receipt-backed, and shift-linked.
- The cash journal remains the working transaction-level refund and anti-fraud
  surface.
- Booking client selection is stable and accessible.
- Ctrl/meta-click can select arbitrary seats while backend interval conflicts
  remain authoritative and group creation remains all-or-nothing.
- Pending reservations require confirmation; confirmed reservations start
  exactly one linked session through an atomic backend command.
- Manual adjustment anti-fraud behavior and immutable inventory economics do
  not regress.
- Fresh verification and whole-branch review find no unresolved Critical or
  Important issue before push.
- No online-booking hold, mobile booking billing picker, or automatic no-show
  implementation enters this epic.
