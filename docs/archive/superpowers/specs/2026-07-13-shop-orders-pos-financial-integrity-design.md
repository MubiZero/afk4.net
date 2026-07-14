# Shop Orders And POS Financial Integrity Design

**Date:** 2026-07-13
**Status:** Approved for implementation planning
**Scope:** Player Shop orders, POS sales, wallet settlement, inventory cost, receipts, and shift reconciliation

## 1. Goal

Make every new Player Shop order a normal, auditable AFK4 sale instead of a
parallel money-and-stock path. A successful order placement must atomically
produce the order, a paid POS sale, a wallet payment, a receipt, inventory
movements for tracked products, and shift linkage.

This slice also corrects inventory costing so POS sales and Player Shop orders
use the product's moving weighted-average cost rather than the retail price or
zero.

## 2. Fixed Product Decisions

- A Player Shop order reserves its money and stock immediately when the player
  places it.
- The reservation is represented as an already-paid POS sale, not as a second
  standalone commerce model.
- The order lifecycle remains `placed -> accepted -> delivered` or
  `placed/accepted -> cancelled`.
- Accepting and delivering an order do not charge money or reduce stock again.
- Delivering an order may accrue loyalty cashback through the existing loyalty
  path.
- Cancelling an order performs the standard POS refund before the order becomes
  cancelled.
- A new Player Shop order requires an open shift. A sale without a shift is not
  allowed.
- Tracked products with insufficient stock reject the order. Non-stock services
  never create stock movements and do not participate in stock-health counts.
- Moving weighted-average cost is the default AFK4 inventory-cost formula for
  interchangeable club goods such as drinks and snacks.

## 3. Why Moving Weighted Average

AFK4 already stores `AvgCostMinorUnits` and recalculates it on purchasing. The
method is appropriate for interchangeable, low-value goods whose individual
units do not need batch or serial tracking. It is also an accepted inventory
cost formula under IAS 2 alongside FIFO; IAS 2 permits recalculation as each
additional shipment is received.

Reference: <https://www.ifrs.org/issued-standards/list-of-standards/ias-2-inventories/>

The accounting invariants are:

- receiving tracked inventory recalculates the moving weighted-average cost;
- a sale snapshots the current average cost into each POS sale line;
- cost of goods sold uses the sale-line cost snapshot;
- a refund reverses the original sale-line cost rather than using the current
  retail price;
- retail price and inventory cost remain separate values;
- services have revenue but no inventory quantity or inventory cost movement;
- AFK4 uses the same cost formula for products with the same nature and use.

FIFO, batch expiry, serial-number costing, and specific identification are not
part of this slice. They can be added later for products that require physical
traceability. Local statutory reporting remains an integration concern and
must be checked separately before AFK4 data is treated as an official ledger.

## 4. Module Boundaries

### Commerce application coordinator

A small application-layer coordinator owns the two cross-module use cases:
placing and cancelling a Player Shop order. It calls Shop, POS/Inventory, and
Billing through their public service interfaces inside one transaction. It
does not own tables or domain rules.

Both the Player Shop cancellation endpoint and an attempted refund of a POS
sale linked to a Shop order route through this coordinator. This prevents a
refunded sale from remaining fulfilment-active and avoids either module
mutating the other's data directly.

### Shop

Shop owns customer-facing order intent and fulfilment state:

- validate an active player session and an orderable catalog;
- coordinate placement through a dedicated commerce orchestration boundary;
- expose the operator queue;
- transition `placed`, `accepted`, `delivered`, and `cancelled`;
- notify Player Shell and Operator App about order changes.

Shop must not directly create wallet ledger entries, POS receipts, payments, or
stock movements after this slice.

### POS And Inventory

POS owns the commercial sale:

- create the sale and immutable sale-line snapshots;
- validate an open shift and product availability;
- settle the sale from the wallet;
- create `PaymentEntity` and `ReceiptEntity` records;
- create stock movements only for lines whose `TrackStock` snapshot is true;
- use the line's cost snapshot for sale and refund stock movements;
- perform idempotent refunds.

Inventory remains the authority for stock-on-hand and moving average cost.

### Billing

Billing owns wallet mutation through immutable ledger entries. POS wallet
settlement uses a Billing-owned operation or narrow service interface; POS and
Shop must not calculate wallet balance by mutating another module's tables
directly.

## 5. Data Model

### ShopOrderEntity

Add nullable `PosSaleId` for migration compatibility, with a foreign key to
`PosSaleEntity` and a unique filtered index for non-null values.

- Every newly created order must have `PosSaleId`.
- One POS sale can belong to at most one shop order.
- Existing rows may remain null and use the legacy cancellation path.
- Historical orders are not backfilled with synthetic receipts.

### PosSaleLineEntity

Add `UnitCostMinorUnits` as an immutable cost snapshot.

- New sale lines copy the product's current `AvgCostMinorUnits` when
  `TrackStock` is true.
- Non-stock service lines store zero cost.
- Refunds use the stored value.
- The existing unit retail price remains in `UnitPriceMinorUnits`.

### Contracts

Expose `PosSaleId` on `ShopOrderDto` so operator and player clients can link an
order to its receipt without reconstructing the relationship.

Add a required `IdempotencyKey` to `PlaceShopOrderRequest` and generate it once
per submit gesture in Player Shell. Retries reuse that value; a new explicit
submit uses a new value. The API rejects an empty key.

Expose the linked `ShopOrderId` in staff POS sale/receipt projections. Player
projections expose only the already-authorized order-to-sale relationship.

Do not expose mutable inventory cost through the player-facing shop catalog.

## 6. Placement Transaction

`PlaceAsync` remains the public Shop application operation, but delegates the
financial sale to a narrow POS commerce service. On relational providers the
whole workflow runs in one serializable transaction:

1. Validate the player and active session.
2. Load active, Player-Shell-enabled products for the session branch.
3. Resolve the current open shift.
4. Aggregate duplicate product lines and validate positive quantities.
5. Validate stock only for `TrackStock && !AllowNegativeStock` products.
6. Validate the spendable wallet balance.
7. Create a POS sale and sale-line price/cost snapshots.
8. Create the wallet debit through Billing.
9. Create the wallet `PaymentEntity`, sale receipt, and tracked stock
   movements.
10. Mark the sale paid.
11. Create the `ShopOrderEntity` linked by `PosSaleId`.
12. Commit, then publish the existing order-created realtime notification.

Any failure before commit leaves no order, sale, payment, receipt, ledger
entry, or stock movement.

The operation is idempotent. Repeating the same request key with the same
payload returns the same order and linked sale. Reusing the key with a different
payload is rejected.

## 7. Order Transitions And Cancellation

`AcceptAsync` and `DeliverAsync` update only fulfilment state and timestamps.
They must not create additional payment or inventory records. Delivery keeps
the existing cashback behavior.

For a new order with `PosSaleId`, the commerce coordinator cancels it as one
transaction:

1. invokes the idempotent POS refund operation for the linked paid sale;
2. creates the wallet reversal/refund entry;
3. creates a negative payment/refund record using the original wallet method;
4. creates tracked-product return movements from sale-line cost snapshots;
5. creates a refund receipt;
6. marks the sale refunded;
7. marks the order cancelled only after the refund succeeds;
8. publishes the order-updated notification after commit.

Repeating cancellation returns the already-cancelled order without creating a
second refund. The direct POS refund endpoint detects a linked active Shop
order and delegates to the same coordinator; it cannot refund the sale alone.

An existing order without `PosSaleId` continues to use the current legacy
wallet-and-stock reversal. That compatibility path is isolated and must never
be used for new orders.

For defensive compatibility, if a linked sale is already refunded by data
created before this rule existed, cancellation only completes the order-state
transition. It does not refund twice.

## 8. Inventory Cost Behavior

- Manual write-off continues to use the current product average cost.
- New POS sale stock movements use the sale-line cost snapshot.
- New Player Shop order stock movements are the linked POS sale movements;
  Shop creates no duplicate movements.
- POS refunds and order cancellations use the original sale-line snapshot.
- Receipt and refund reports use retail totals for revenue and cost snapshots
  for cost-of-goods reporting.
- Returning inventory at an original cost different from the current average
  must update inventory carrying value consistently. The implementation plan
  must introduce one Inventory-owned helper for inbound-value reconciliation
  rather than modifying `AvgCostMinorUnits` from POS or Shop.

## 9. Error And Concurrency Semantics

- No open shift: reject with an actionable `open_shift_required` error.
- Insufficient wallet: reject with `insufficient_funds`.
- Insufficient tracked stock: reject with `out_of_stock`.
- Inactive or unavailable product: reject with `product_unavailable`.
- Mixed currency: reject before creating any records.
- Concurrent attempts to buy the last unit serialize; only one succeeds.
- A stale order transition returns the existing version-conflict response.
- A failed refund leaves the order in its previous state.
- Realtime notification failure after commit does not roll back committed
  finance records; the existing reload/reconciliation paths recover the UI.

## 10. Operator And Player Experience

This slice does not redesign the order ticker.

- The order appears in the existing operator queue after placement.
- The linked paid sale appears in `Касса -> Журнал кассы -> Чеки`.
- The sale participates in current-shift revenue and reconciliation.
- Opening the linked receipt shows the same product lines and total as the
  Player Shop order.
- Cancelling the order or refunding the linked receipt invokes the same
  commerce-cancellation operation and converges on the same order and financial
  state.
- The stock journal shows one sale movement per tracked line and one return
  movement per refunded tracked line, both with real cost.

## 11. Verification Strategy

Implementation follows strict RED-GREEN-REFACTOR TDD.

Service tests must prove:

- placement creates one linked paid POS sale, wallet payment, receipt, ledger
  debit, order, and shift link;
- tracked products create stock movements with average cost;
- non-stock services create no stock movement;
- insufficient funds, missing shift, invalid products, and insufficient stock
  leave no partial records;
- idempotent replay does not duplicate records;
- concurrent purchase of the last tracked unit permits one success;
- accept and deliver do not mutate payment or stock again;
- cancellation creates one POS refund, wallet reversal, refund receipt, and
  tracked-stock return;
- repeated cancellation and receipt-refund-then-cancel do not double-refund;
- legacy orders without `PosSaleId` remain cancellable;
- shop, sales, receipt, stock, and shift reports project the linked transaction
  consistently.

Contract and endpoint tests must prove the new `PosSaleId` relationship and
stable error codes. Existing POS, Shop, Inventory, Billing, Shift, receipt,
realtime, loyalty, and migration tests remain green.

## 12. Out Of Scope

- Mixed POS payments and the unified Operator payment dialog backend.
- Starting a session atomically from a reservation.
- Reservation money holds and no-show processing.
- Cash-journal navigation changes.
- Order-ticker visual polish.
- Price/balance labels and booking Ctrl/Cmd multi-selection.
- FIFO, expiry batches, serial numbers, and country-specific fiscal providers.
