# Shop Orders And POS Financial Integrity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make every new Player Shop order an idempotent, paid POS sale linked to the open shift, wallet ledger, receipt, and correctly costed inventory movements, with cancellation converging on the standard POS refund path.

**Architecture:** Keep Shop as the owner of fulfilment state, POS/Inventory as the owner of sales and stock, and Billing as the owner of wallet ledger entries. A scoped commerce coordinator opens one serializable transaction and asks narrow module services to stage placement or refund mutations in the shared `PlatformDbContext`; notifications run only after commit. Existing unlinked orders retain the isolated legacy cancellation path.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core/PostgreSQL, xUnit, React 19, TypeScript, Bun test.

## Global Constraints

- Follow strict RED-GREEN-REFACTOR TDD for contracts, domain rules, endpoints, and Player Shell behavior.
- A new order is committed atomically with one paid POS sale, wallet debit, wallet payment, sale receipt, stock movements, and open-shift linkage.
- A linked cancellation is committed atomically with one wallet reversal, refund payment, refund receipt, stock returns, refunded sale, and cancelled order.
- Shop must not directly construct financial or inventory entities after the refactor.
- POS and Shop must not mutate wallet ledger rows directly; they use the Billing-owned wallet settlement service.
- Only `TrackStock` lines create stock movements; `AllowNegativeStock` bypasses availability rejection but not movement creation.
- Sale and refund movements use the immutable `PosSaleLineEntity.UnitCostMinorUnits` snapshot, never retail price.
- New placement requires a non-empty idempotency key and an open shift.
- Existing `ShopOrderEntity` rows with null `PosSaleId` remain cancellable through the legacy path.
- Keep transport DTOs in `AFK4.Shared.Contracts`; domain/data entities remain in `AFK4.Platform.Api`.
- Do not update progress or roadmap until implementation state and verification actually change.

---

## File Map

- `src/AFK4.Shared.Contracts/Shop/PlaceShopOrderRequest.cs` — placement idempotency contract.
- `src/AFK4.Shared.Contracts/Shop/ShopOrderDto.cs` — order-to-sale link projection.
- `src/AFK4.Shared.Contracts/Pos/PosSaleDto.cs` and `src/AFK4.Shared.Contracts/Receipts/ReceiptDto.cs` — staff-facing sale/receipt-to-order link.
- `src/AFK4.Platform.Api/Data/ShopOrderEntity.cs` and `PosSaleLineEntity.cs` — persisted link and cost snapshot.
- `src/AFK4.Platform.Api/Data/PlatformDbContext.cs` plus generated migration files — nullable legacy-safe FK, unique filtered index, and cost column.
- `src/AFK4.Platform.Api/Inventory/IInventoryCostService.cs` and `EfInventoryCostService.cs` — Inventory-owned inbound-value reconciliation.
- `src/AFK4.Platform.Api/Billing/IWalletSettlementService.cs` and `EfWalletSettlementService.cs` — Billing-owned debit/reversal staging.
- `src/AFK4.Platform.Api/Pos/IShopPosSettlementService.cs` and `EfShopPosSettlementService.cs` — paid wallet sale and refund staging for Shop commerce.
- `src/AFK4.Platform.Api/Commerce/IShopCommerceCoordinator.cs` and `EfShopCommerceCoordinator.cs` — serializable cross-module transaction, idempotency, and notification boundary.
- `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs` — fulfilment transitions, projections, and legacy cancellation only; delegates linked place/cancel.
- `src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs`, `ShopOrderEndpoints.cs`, and `PosEndpoints.cs` — stable errors and convergent cancellation/refund routing.
- `src/AFK4.Player.Shell.Web/src/shellApi.ts` and `apiTypes.ts` — one placement key per submit gesture and mirrored DTOs.
- Focused tests under `tests/AFK4.Platform.Api.Tests/Shop/`, `tests/AFK4.Platform.Api.Tests/`, `tests/AFK4.Shared.Contracts.Tests/`, and `src/AFK4.Player.Shell.Web/src/` prove each boundary.

---

### Task 1: Persisted Relationships And Transport Contracts

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/ShopOrderEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PosSaleLineEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Modify: `src/AFK4.Shared.Contracts/Shop/PlaceShopOrderRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Shop/ShopOrderDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Pos/PosSaleDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Receipts/ReceiptDto.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/PosContractSerializationTests.cs`
- Create: `tests/AFK4.Shared.Contracts.Tests/ShopContractSerializationTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/PlatformDbContextDesignTimeFactoryTests.cs`
- Generate: `src/AFK4.Platform.Api/Data/Migrations/` migration files and update `PlatformDbContextModelSnapshot.cs`

**Interfaces:**
- Produces: `PlaceShopOrderRequest(IReadOnlyList<ShopOrderLineInput> Lines, string IdempotencyKey)`.
- Produces: nullable `ShopOrderEntity.PosSaleId` and immutable `PosSaleLineEntity.UnitCostMinorUnits`.
- Produces: optional `PosSaleId`/`ShopOrderId` projection properties for legacy compatibility.

- [ ] **Step 1: Write failing contract and model tests**

Add serialization assertions that require the stable wire names:

```csharp
[Fact]
public void PlaceShopOrderRequest_RoundTripsIdempotencyKey()
{
    var request = new PlaceShopOrderRequest(
        [new ShopOrderLineInput(Guid.Parse("dddddddd-0000-0000-0000-000000000001"), 2)],
        "shop-place-001");

    var json = JsonSerializer.Serialize(request, Options);
    var copy = JsonSerializer.Deserialize<PlaceShopOrderRequest>(json, Options);

    Assert.Equal("shop-place-001", copy!.IdempotencyKey);
    Assert.Contains("\"idempotencyKey\"", json);
}

[Fact]
public void ShopAndPosDtos_RoundTripLinkedIds()
{
    var saleId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
    var orderId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
    var branchId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    var playerId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    var order = new ShopOrderDto(orderId, branchId, Guid.NewGuid(), playerId, "Player",
        ShopOrderStatusNames.Placed, new MoneyDto("TJS", 500), [],
        DateTimeOffset.Parse("2026-07-13T10:00:00Z"), null, null, null, 1, saleId);
    var sale = new PosSaleDto(saleId, Guid.NewGuid(), branchId, Guid.NewGuid(),
        PosSaleStateNames.Paid, [], new MoneyDto("TJS", 500), Guid.Empty,
        DateTimeOffset.Parse("2026-07-13T10:00:00Z"),
        DateTimeOffset.Parse("2026-07-13T10:00:00Z"), null, null,
        LatestReceipt: null, PlayerAccountId: playerId, ShopOrderId: orderId);

    var orderCopy = JsonSerializer.Deserialize<ShopOrderDto>(JsonSerializer.Serialize(order, Options), Options);
    var saleCopy = JsonSerializer.Deserialize<PosSaleDto>(JsonSerializer.Serialize(sale, Options), Options);

    Assert.Equal(saleId, orderCopy!.PosSaleId);
    Assert.Equal(orderId, saleCopy!.ShopOrderId);
}
```

Add an EF model assertion in `PlatformDbContextDesignTimeFactoryTests.cs` that `ShopOrderEntity.PosSaleId` has a unique filtered index and a foreign key to `PosSaleEntity`, and that `PosSaleLineEntity.UnitCostMinorUnits` is non-nullable.

- [ ] **Step 2: Run the tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "ShopContractSerializationTests|PosContractSerializationTests"
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PlatformDbContextDesignTimeFactoryTests
```

Expected: compilation failures because the new constructor parameters and entity properties do not exist.

- [ ] **Step 3: Add the schema and contract fields**

Use these exact shapes, keeping nullable defaults at the end of DTO constructors:

```csharp
public sealed record PlaceShopOrderRequest(
    IReadOnlyList<ShopOrderLineInput> Lines,
    string IdempotencyKey);

// ShopOrderEntity
public Guid? PosSaleId { get; set; }

// PosSaleLineEntity
public long UnitCostMinorUnits { get; set; }

// append to ShopOrderDto
Guid? PosSaleId = null

// append to PosSaleDto and ReceiptDto
Guid? ShopOrderId = null
```

Configure the relationship without backfilling legacy rows:

```csharp
entity.HasOne<PosSaleEntity>()
    .WithOne()
    .HasForeignKey<ShopOrderEntity>(order => order.PosSaleId)
    .OnDelete(DeleteBehavior.Restrict);
entity.HasIndex(order => order.PosSaleId)
    .IsUnique()
    .HasFilter("\"PosSaleId\" IS NOT NULL");
```

- [ ] **Step 4: Generate and inspect the migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add LinkShopOrdersToPosSales --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

Expected migration behavior: add nullable `PosSaleId` to `shop_orders`, add non-null `UnitCostMinorUnits` with default `0` to `pos_sale_lines`, create the FK, and create a unique filtered index. Inspect the generated migration and snapshot; do not hand-edit the designer unless scaffolding is unavailable.

- [ ] **Step 5: Run GREEN verification and commit**

Run the two test commands from Step 2 plus:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --no-restore
```

Expected: all selected tests pass and build reports 0 errors.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts src/AFK4.Platform.Api/Data tests/AFK4.Shared.Contracts.Tests tests/AFK4.Platform.Api.Tests/PlatformDbContextDesignTimeFactoryTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(commerce): link shop orders to POS sales"
```

---

### Task 2: Inventory Cost Snapshots And Inbound Reconciliation

**Files:**
- Create: `src/AFK4.Platform.Api/Inventory/IInventoryCostService.cs`
- Create: `src/AFK4.Platform.Api/Inventory/EfInventoryCostService.cs`
- Modify: `src/AFK4.Platform.Api/Inventory/EfInventoryService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfPosService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfPosServiceTests.cs`

**Interfaces:**
- Produces: `long MovingWeightedAverage(long currentQuantity, long currentUnitCost, int inboundQuantity, long inboundUnitCost)`.
- Produces: `Task ReconcileInboundAsync(Guid organizationId, Guid branchId, Guid productId, int quantity, long unitCostMinorUnits, CancellationToken cancellationToken)`.
- Consumes: `PosSaleLineEntity.UnitCostMinorUnits` from Task 1.

- [ ] **Step 1: Write failing cost tests**

Add tests with explicit accounting values:

```csharp
[Fact]
public void MovingWeightedAverage_UsesCurrentCarryingValueAndInboundValue()
{
    Assert.Equal(550, EfInventoryCostService.MovingWeightedAverage(10, 400, 30, 600));
}

[Fact]
public async Task PaySaleAsync_UsesAverageCostForTrackedLineAndZeroForService()
{
    var tracked = await SeedProductAsync(trackStock: true, avgCostMinorUnits: 275);
    var serviceProduct = await SeedProductAsync(trackStock: false, avgCostMinorUnits: 999);
    var paidSale = await CreateAndPayAsync(tracked.ProductId, serviceProduct.ProductId);
    var trackedLine = paidSale.Lines.Single(line => line.ProductId == tracked.ProductId);
    var serviceLine = paidSale.Lines.Single(line => line.ProductId == serviceProduct.ProductId);
    var movements = await db.StockMovements.ToListAsync();
    var saleMovement = movements.Single(movement => movement.ProductId == tracked.ProductId);

    Assert.Equal(275, trackedLine.UnitCostMinorUnits);
    Assert.Equal(275, saleMovement.UnitCostMinorUnits);
    Assert.Equal(0, serviceLine.UnitCostMinorUnits);
    Assert.DoesNotContain(movements, movement => movement.ProductId == serviceProduct.ProductId);
}

[Fact]
public async Task RefundSaleAsync_ReusesOriginalCostAndReconcilesCurrentAverage()
{
    var product = await SeedProductAsync(trackStock: true, avgCostMinorUnits: 400, stockOnHand: 10);
    var sale = await CreateAndPayAsync(product.ProductId);
    await ReceiveStockAsync(product.ProductId, quantity: 30, unitCostMinorUnits: 600);
    await service.RefundSaleAsync(sale.PosSaleId, StaffId,
        new RefundPosSaleRequest(OrganizationId, "return", "refund-cost-001"), CancellationToken.None);
    var refundMovement = await db.StockMovements.SingleAsync(movement =>
        movement.ProductId == product.ProductId && movement.MovementType == StockMovementTypeNames.Refund);
    product = await db.PosProducts.SingleAsync(candidate => candidate.ProductId == product.ProductId);

    Assert.Equal(400, refundMovement.UnitCostMinorUnits);
    Assert.Equal(550, product.AvgCostMinorUnits);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfInventoryServiceTests|EfPosServiceTests"
```

Expected: failures show sale/refund movements still use retail price and the helper does not exist.

- [ ] **Step 3: Implement the Inventory-owned formula and reuse it for purchases**

```csharp
public interface IInventoryCostService
{
    Task ReconcileInboundAsync(Guid organizationId, Guid branchId, Guid productId,
        int quantity, long unitCostMinorUnits, CancellationToken cancellationToken);
}

public static long MovingWeightedAverage(
    long currentQuantity, long currentUnitCost, int inboundQuantity, long inboundUnitCost)
{
    var baseQuantity = Math.Max(currentQuantity, 0);
    var denominator = checked(baseQuantity + inboundQuantity);
    return denominator <= 0
        ? inboundUnitCost
        : (long)Math.Round(
            (baseQuantity * (double)currentUnitCost + inboundQuantity * (double)inboundUnitCost) / denominator,
            MidpointRounding.AwayFromZero);
}
```

`ReconcileInboundAsync` must load the tracked product in the requested organization/branch, sum current movement quantity before the new return is persisted, assign the calculated average, and throw for a missing/non-stock product. Replace the duplicated purchase formula in `EfInventoryService` with the same static helper.

- [ ] **Step 4: Snapshot cost when creating POS lines and use it for sale/refund**

In `EfPosService.CreateSaleAsync`:

```csharp
UnitCostMinorUnits = product.TrackStock ? product.AvgCostMinorUnits : 0,
```

In both stock movement loops:

```csharp
UnitCostMinorUnits = line.UnitCostMinorUnits,
```

Before adding each refund movement, call `ReconcileInboundAsync` using the line snapshot. Register `IInventoryCostService` as scoped in `Program.cs`.

- [ ] **Step 5: Run GREEN verification and commit**

Run the focused command from Step 2. Expected: all `EfInventoryServiceTests` and `EfPosServiceTests` pass.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api/Inventory src/AFK4.Platform.Api/Pos/EfPosService.cs src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/EfInventoryServiceTests.cs tests/AFK4.Platform.Api.Tests/EfPosServiceTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "fix(inventory): preserve weighted cost through POS refunds"
```

---

### Task 3: Billing-Owned Wallet Settlement Boundary

**Files:**
- Create: `src/AFK4.Platform.Api/Billing/IWalletSettlementService.cs`
- Create: `src/AFK4.Platform.Api/Billing/EfWalletSettlementService.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Billing/EfWalletSettlementServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`

**Interfaces:**
- Produces: `WalletSettlementResult` with `Succeeded`, stable `ErrorCode`, and `LedgerEntryEntity? Entry`.
- Produces: `DebitAsync(Guid organizationId, Guid branchId, Guid playerAccountId, Guid? sessionId, Guid shiftId, long amountMinorUnits, string currencyCode, string description, string reason, Guid actorStaffUserId, DateTimeOffset now, CancellationToken cancellationToken)`.
- Produces: `ReverseAsync(LedgerEntryEntity originalDebit, Guid actorStaffUserId, string description, string reason, DateTimeOffset now, CancellationToken cancellationToken)`; neither method starts a transaction nor calls `SaveChangesAsync`.

- [ ] **Step 1: Write failing debit and reversal tests**

```csharp
[Fact]
public async Task DebitAsync_StagesWalletPaymentWithShiftAndReference()
{
    var result = await service.DebitAsync(org, branch, player, session, shift, 1500, "TJS",
        "shop_order", orderId.ToString("D"), Guid.Empty, now, CancellationToken.None);
    Assert.True(result.Succeeded);
    Assert.Equal(-1500, result.Entry!.AmountMinorUnits);
    Assert.Equal(shift, result.Entry.ShiftId);
    Assert.Equal(LedgerEntryTypeNames.WalletPayment, result.Entry.EntryType);
}

[Fact]
public async Task DebitAsync_InsufficientBalance_StagesNothing()
{
    var result = await service.DebitAsync(org, branch, player, session, shift, 1501, "TJS",
        "shop_order", orderId.ToString("D"), Guid.Empty, now, CancellationToken.None);
    Assert.Equal("insufficient_funds", result.ErrorCode);
    Assert.Empty(db.ChangeTracker.Entries<LedgerEntryEntity>().Where(entry => entry.State == EntityState.Added));
}

[Fact]
public async Task ReverseAsync_StagesOneReversalOfOriginalDebit()
{
    var result = await service.ReverseAsync(originalDebit, staff, "shop_order_cancel",
        orderId.ToString("D"), now, CancellationToken.None);
    Assert.Equal(originalDebit.LedgerEntryId, result.Entry!.ReversesLedgerEntryId);
    Assert.Equal(1500, result.Entry.AmountMinorUnits);
}
```

- [ ] **Step 2: Run tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfWalletSettlementServiceTests
```

Expected: compilation failure because the Billing-owned service does not exist.

- [ ] **Step 3: Implement the staging boundary**

```csharp
public sealed record WalletSettlementResult(bool Succeeded, string? ErrorCode, LedgerEntryEntity? Entry)
{
    public static WalletSettlementResult Ok(LedgerEntryEntity entry) => new(true, null, entry);
    public static WalletSettlementResult Reject(string code) => new(false, code, null);
}
```

`DebitAsync` must validate player/currency, sum wallet entries inside the caller transaction, return `insufficient_funds` when needed, create the entry through `BillingEntryFactory`, attach `ShiftId`, and add it to `dbContext.LedgerEntries`. `ReverseAsync` must reject an already-reversed debit and stage exactly one `Reversal` entry. Register the service in `Program.cs`.

- [ ] **Step 4: Run GREEN verification and commit**

Run the focused command from Step 2. Expected: all wallet settlement tests pass.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api/Billing src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Billing/EfWalletSettlementServiceTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(billing): add wallet settlement boundary"
```

---

### Task 4: POS Settlement For Player Shop Orders

**Files:**
- Create: `src/AFK4.Platform.Api/Pos/IShopPosSettlementService.cs`
- Create: `src/AFK4.Platform.Api/Pos/EfShopPosSettlementService.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`

**Interfaces:**
- Consumes: `IWalletSettlementService`, `IInventoryCostService`, `IReceiptNumberGenerator`.
- Produces: `ShopPosSettlementResult` with stable error code, `PosSaleEntity`, lines, debit entry, and receipt.
- Produces: `CreatePaidWalletSaleAsync(ShopPosSaleRequest request, CancellationToken)` and `RefundPaidWalletSaleAsync(ShopPosRefundRequest request, CancellationToken)`.
- Constraint: methods only stage entities; they never call `SaveChangesAsync`, begin a transaction, or commit a transaction.

- [ ] **Step 1: Write failing paid-sale tests**

```csharp
[Fact]
public async Task CreatePaidWalletSaleAsync_StagesCompleteShiftLinkedSale()
{
    var result = await service.CreatePaidWalletSaleAsync(request, CancellationToken.None);
    Assert.True(result.Succeeded);
    Assert.Equal(PosSaleStateNames.Paid, result.Sale!.State);
    Assert.Equal(openShiftId, result.Sale.ShiftId);
    Assert.Equal(PaymentMethodNames.Wallet, db.Payments.Single().PaymentMethod);
    Assert.Equal("sale", db.Receipts.Single().ReceiptType);
    Assert.Equal(275, db.StockMovements.Single().UnitCostMinorUnits);
}

[Theory]
[InlineData(false, true, "open_shift_required")]
[InlineData(true, false, "insufficient_funds")]
public async Task CreatePaidWalletSaleAsync_Failure_StagesNoPartialFinance(
    bool hasOpenShift, bool hasFunds, string expectedCode)
{
    var result = await ExecuteScenario(hasOpenShift, hasFunds);
    Assert.Equal(expectedCode, result.ErrorCode);
    Assert.Empty(db.PosSales);
    Assert.Empty(db.Payments);
    Assert.Empty(db.Receipts);
}
```

Add these named cases to the same class, each asserting the stable code and the exact staged-record counts: `CreatePaidWalletSaleAsync_OutOfStock_ReturnsOutOfStock`, `CreatePaidWalletSaleAsync_InactiveProduct_ReturnsProductUnavailable`, `CreatePaidWalletSaleAsync_MixedCurrency_ReturnsMixedCurrency`, `CreatePaidWalletSaleAsync_NonStockService_CreatesNoMovement`, and `CreatePaidWalletSaleAsync_DuplicateLines_AggregatesQuantityAndTotal`.

- [ ] **Step 2: Run the paid-sale tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfShopPosSettlementServiceTests
```

Expected: compilation failure because the settlement interface and request/result records do not exist.

- [ ] **Step 3: Implement paid wallet sale staging**

Use focused internal records:

```csharp
public sealed record ShopPosSaleRequest(
    Guid OrganizationId, Guid BranchId, Guid PlayerAccountId, Guid SessionId,
    Guid ActorStaffUserId, IReadOnlyList<ShopOrderLineInput> Lines,
    string ReferenceId, DateTimeOffset Now);

public sealed record ShopPosRefundRequest(
    Guid PosSaleId, Guid ShopOrderId, Guid ActorStaffUserId,
    string Reason, DateTimeOffset Now);

public sealed record ShopPosSettlementResult(
    bool Succeeded,
    string? ErrorCode,
    PosSaleEntity? Sale,
    IReadOnlyList<PosSaleLineEntity> Lines,
    LedgerEntryEntity? WalletEntry,
    ReceiptEntity? Receipt)
{
    public static ShopPosSettlementResult Reject(string code) =>
        new(false, code, null, [], null, null);
}
```

The create method must resolve the current open shift, load active shell-enabled products, aggregate duplicate quantities, validate currency/stock, snapshot price and average cost, call `DebitAsync`, and stage:

```csharp
new PaymentEntity {
    PaymentKind = "payment", Provider = "wallet", PaymentMethod = PaymentMethodNames.Wallet,
    AmountMinorUnits = sale.TotalMinorUnits, PosSaleId = sale.PosSaleId, ShiftId = sale.ShiftId
};
```

It must create one sale receipt and one negative stock movement per tracked aggregated line, mark the sale paid immediately, and return all linked objects.

- [ ] **Step 4: Write failing linked-refund tests**

```csharp
[Fact]
public async Task RefundPaidWalletSaleAsync_StagesFinancialAndStockReversalOnce()
{
    var result = await service.RefundPaidWalletSaleAsync(request, CancellationToken.None);
    Assert.True(result.Succeeded);
    Assert.Equal(PosSaleStateNames.Refunded, result.Sale!.State);
    Assert.Single(db.Payments.Where(payment => payment.PaymentKind == "refund"));
    Assert.Single(db.Receipts.Where(receipt => receipt.ReceiptType == "refund"));
    Assert.Equal(originalLine.UnitCostMinorUnits,
        db.StockMovements.Single(movement => movement.MovementType == StockMovementTypeNames.Refund).UnitCostMinorUnits);
}
```

Add cases for already-refunded sale returning success without duplicates and non-paid sale returning `sale_not_refundable`.

- [ ] **Step 5: Implement refund staging, run GREEN, and commit**

The refund method must call Billing reversal, add a negative wallet `PaymentEntity`, generate a refund receipt, reconcile inbound inventory value and add positive return movements from cost snapshots, then mark the sale refunded. Register the service.

Run the Task 4 focused command. Expected: every create/refund settlement case passes.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api/Pos src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(pos): settle player shop orders as paid sales"
```

---

### Task 5: Serializable Commerce Coordinator And Shop Refactor

**Files:**
- Create: `src/AFK4.Platform.Api/Commerce/IShopCommerceCoordinator.cs`
- Create: `src/AFK4.Platform.Api/Commerce/EfShopCommerceCoordinator.cs`
- Create: `src/AFK4.Platform.Api/Shop/IShopOrderWorkflow.cs`
- Create: `src/AFK4.Platform.Api/Shop/EfShopOrderWorkflow.cs`
- Modify: `src/AFK4.Platform.Api/Shop/IShopOrderService.cs`
- Modify: `src/AFK4.Platform.Api/Shop/EfShopOrderService.cs`
- Modify: `src/AFK4.Platform.Api/Shop/ShopOrderProjection.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServicePlaceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/EfShopOrderServiceTransitionTests.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Shop/EfShopCommerceCoordinatorTests.cs`

**Interfaces:**
- Produces: coordinator `PlaceAsync(Guid playerAccountId, PlaceShopOrderRequest request, CancellationToken)`.
- Produces: coordinator operator/player cancellation methods and linked POS-refund method.
- Produces: Shop-owned `IShopOrderWorkflow` for resolving player/session context, creating/projecting an order, applying fulfilment transitions, and isolating legacy cancellation.
- `IShopOrderService.PlaceAsync` changes to accept the full request so idempotency is never dropped.

- [ ] **Step 1: Rewrite placement tests to describe the complete atomic result**

```csharp
[Fact]
public async Task PlaceAsync_CreatesOneLinkedPaidSaleAndOneOrder()
{
    var result = await service.PlaceAsync(player,
        new PlaceShopOrderRequest([new ShopOrderLineInput(product, 3)], "place-001"),
        CancellationToken.None);

    Assert.True(result.Succeeded);
    var order = Assert.Single(db.ShopOrders);
    var sale = Assert.Single(db.PosSales);
    Assert.Equal(sale.PosSaleId, order.PosSaleId);
    Assert.Equal(sale.PosSaleId, result.Order!.PosSaleId);
    Assert.Equal(PosSaleStateNames.Paid, sale.State);
    Assert.Single(db.Payments);
    Assert.Single(db.Receipts);
}
```

Add named tests `PlaceAsync_MissingShift_LeavesNoRecords`, `PlaceAsync_InsufficientFunds_LeavesNoRecords`, `PlaceAsync_OutOfStock_LeavesNoRecords`, `PlaceAsync_UnavailableProduct_LeavesNoRecords`, `PlaceAsync_Replay_ReturnsOriginalOrder`, `PlaceAsync_ReusedKeyWithChangedLines_ReturnsConflict`, and `AcceptAndDeliver_DoNotRepeatSettlement`. In every rejection test assert zero new rows in `ShopOrders`, `PosSales`, `Payments`, `Receipts`, `LedgerEntries`, and `StockMovements`.

- [ ] **Step 2: Run Shop service tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfShopOrderServicePlaceTests|EfShopOrderServiceTransitionTests|EfShopCommerceCoordinatorTests"
```

Expected: constructor/signature failures and missing linked finance records.

- [ ] **Step 3: Implement coordinator transaction and placement idempotency**

Use this public boundary:

```csharp
public interface IShopCommerceCoordinator
{
    Task<ShopOrderActionResult> PlaceAsync(Guid playerAccountId, PlaceShopOrderRequest request, CancellationToken cancellationToken);
    Task<ShopOrderActionResult> CancelByOperatorAsync(Guid branchId, Guid orderId, Guid staffUserId,
        int? expectedVersion, CancellationToken cancellationToken);
    Task<ShopOrderActionResult> CancelByPlayerAsync(Guid playerAccountId, Guid orderId, CancellationToken cancellationToken);
    Task<BillingCommandServiceResult<PosSaleDto>> RefundLinkedSaleAsync(Guid saleId, Guid staffUserId,
        RefundPosSaleRequest request, CancellationToken cancellationToken);
}
```

Use this Shop-owned persistence boundary so Commerce never writes Shop tables directly:

```csharp
public interface IShopOrderWorkflow
{
    Task<ShopPlacementContextResult> ResolvePlacementContextAsync(
        Guid playerAccountId, CancellationToken cancellationToken);
    Task<ShopOrderDto> CreatePlacedAsync(
        ShopPlacementContext context, ShopPosSettlementResult settlement,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<ShopCancellationContextResult> ResolveOperatorCancellationAsync(
        Guid branchId, Guid orderId, int? expectedVersion, CancellationToken cancellationToken);
    Task<ShopCancellationContextResult> ResolvePlayerCancellationAsync(
        Guid playerAccountId, Guid orderId, CancellationToken cancellationToken);
    Task<ShopOrderDto> MarkCancelledAsync(
        ShopOrderEntity order, Guid actorStaffUserId, DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<ShopOrderActionResult> CancelLegacyAsync(
        ShopOrderEntity order, Guid actorStaffUserId, CancellationToken cancellationToken);
}

public sealed record ShopPlacementContext(
    Guid OrganizationId, Guid BranchId, Guid PlayerAccountId,
    Guid SessionId, Guid SeatId, string PlayerDisplayName);

public sealed record ShopPlacementContextResult(
    bool Succeeded, string? ErrorCode, ShopPlacementContext? Context);

public sealed record ShopCancellationContextResult(
    bool Succeeded, bool NotFound, bool Conflict, string? ErrorCode,
    int? CurrentVersion, ShopOrderEntity? Order);
```

`EfShopOrderWorkflow` owns all Shop table reads/writes and projections. `EfShopOrderService` becomes a thin public facade: place and cancel delegate to `IShopCommerceCoordinator`; list/accept/deliver delegate to the workflow. The coordinator validates the placement key, hashes `{ PlayerAccountId, request.Lines }`, replays/rejects through `BillingCommandIdempotencyEntity` operation `shop-order-place`, opens `IsolationLevel.Serializable` on relational providers, asks the workflow for active player/session context, calls `IShopPosSettlementService.CreatePaidWalletSaleAsync`, asks the workflow to create the linked order, persists the `ShopOrderDto` in the idempotency record, and commits before `IShopOrderNotifier.NotifyCreatedAsync`.

Canonicalize idempotency input by grouping duplicate product IDs, summing quantities, and ordering by product ID before hashing. Recover a duplicate idempotency-record insert by clearing the tracker and replaying the committed record. On PostgreSQL SQLSTATE `40001` (`SerializationFailure`), roll back, clear the tracker, and retry the complete unit up to three times; the losing last-unit attempt then re-reads stock and returns `out_of_stock` instead of leaking a provider exception.

Publish notifications in a `try/catch` after commit. Log notifier failures through `ILogger<EfShopCommerceCoordinator>` and still return the committed success result so realtime transport cannot turn durable finance into an apparent failed placement.

For in-memory tests, execute the same unit without `BeginTransactionAsync`. On failure, clear tracked staged entities before returning so a later `SaveChangesAsync` cannot persist partial work.

- [ ] **Step 4: Move linked cancellation into the coordinator**

For `PosSaleId != null`, call the settlement refund inside the same serializable transaction and ask the workflow to mark the order cancelled only after it succeeds. For `PosSaleId == null`, call `IShopOrderWorkflow.CancelLegacyAsync`, containing the existing wallet/stock reversal. Repeated cancellation returns the existing cancelled DTO without new mutations. Keep accept/deliver and cashback behavior in `EfShopOrderWorkflow`; they only alter fulfilment state.

For `RefundLinkedSaleAsync`, preserve the existing POS refund idempotency contract: hash `{ PosSaleId, request }`, use operation `pos-sale-refund`, replay the same `PosSaleDto`, and return `idempotency_conflict` when the same key carries a different reason or organization. Order-initiated cancellation derives the stable internal reference `shop-order-cancel:{orderId:D}` and relies on the persisted sale/order states to prevent duplicate reversals.

- [ ] **Step 5: Run GREEN verification and commit**

Run the focused command from Step 2. Expected: all placement, transition, cancellation, idempotency, and no-partial-write tests pass.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api/Commerce src/AFK4.Platform.Api/Shop src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/Shop
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(commerce): coordinate atomic shop placement and cancellation"
```

---

### Task 6: Endpoint Errors And Refund Convergence

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerShopEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/ShopOrderEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Dtos.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfPosService.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/PlayerShopEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/ShopOrderEndpointTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/PosEndpointTests.cs`

**Interfaces:**
- Consumes: `IShopCommerceCoordinator` from Task 5.
- Produces stable HTTP error bodies: `open_shift_required`, `insufficient_funds`, `out_of_stock`, `product_unavailable`, `mixed_currency`, `idempotency_key_required`, and `idempotency_conflict`.

- [ ] **Step 1: Write failing endpoint tests**

```csharp
[Fact]
public async Task PlaceOrder_WithoutOpenShift_Returns409OpenShiftRequired()
{
    var response = await client.PostAsJsonAsync("/api/me/shop/orders",
        new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 1)], "place-no-shift"));
    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.Equal("open_shift_required", (await response.Content.ReadFromJsonAsync<ShopErrorBody>())!.Error);
}

[Fact]
public async Task RefundLinkedReceipt_CancelsOrderAndRefundsSaleOnce()
{
    var first = await staffClient.PostAsJsonAsync($"/api/pos/sales/{saleId:D}/refunds", request);
    var second = await staffClient.PostAsJsonAsync($"/api/pos/sales/{saleId:D}/refunds", request);
    Assert.Equal(HttpStatusCode.OK, first.StatusCode);
    Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    Assert.Equal(ShopOrderStatusNames.Cancelled, db.ShopOrders.Single().Status);
    Assert.Single(db.Payments.Where(payment => payment.PaymentKind == "refund"));
}
```

Add named endpoint cases `PlaceOrder_EmptyKey_ReturnsIdempotencyKeyRequired`, `PlaceOrder_ReusedKeyWithChangedLines_ReturnsIdempotencyConflict`, `CancelOrder_ByPlayer_RefundsLinkedSale`, `CancelOrder_ByOperator_RefundsLinkedSale`, and `CancelLegacyOrder_UsesCompatibilityPath`. Each cancellation case must assert the response status plus persisted order, sale, payment, receipt, ledger, and movement counts.

- [ ] **Step 2: Run endpoint tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "PlayerShopEndpointTests|ShopOrderEndpointTests|PosEndpointTests"
```

Expected: failures because seed data has no open shift/key and POS refunds bypass Shop.

- [ ] **Step 3: Route placement/cancellation and linked refunds through the coordinator**

Pass the complete request from `PlayerShopEndpoints`. Use the coordinator for both cancellation endpoints. In the POS refund endpoint, call `RefundLinkedSaleAsync`; the coordinator delegates unlinked sales to `IPosService.RefundSaleAsync` and handles linked sales atomically. Map business errors without replacing stable codes with human-readable strings.

Update `ShopTestSeed.SeedActivePlayerWithProductsAsync` to create an open shift for success scenarios, and let missing-shift tests explicitly remove it.

- [ ] **Step 4: Project linked IDs on sales and receipts**

When `EfPosService.GetSaleAsync` projects a staff sale, query the optional Shop order by `PosSaleId` and pass `ShopOrderId` into both `PosSaleDto` and its `LatestReceipt`. Change `EndpointHelpers.ToDto(ReceiptEntity receipt, Guid? shopOrderId = null)` and make the standalone `/api/receipts/{receiptId}` endpoint resolve the optional order by the receipt's `PosSaleId` before projecting it. Keep player Shop responses limited to their authorized `PosSaleId` relationship.

- [ ] **Step 5: Run GREEN verification and commit**

Run the focused command from Step 2. Expected: all endpoint tests pass with stable codes and convergent refund state.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api/Endpoints src/AFK4.Platform.Api/Pos/EfPosService.cs tests/AFK4.Platform.Api.Tests/Shop tests/AFK4.Platform.Api.Tests/PosEndpointTests.cs
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(api): converge shop cancellation and POS refunds"
```

---

### Task 7: Player Shell Placement Idempotency

**Files:**
- Modify: `src/AFK4.Player.Shell.Web/src/apiTypes.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/shellApi.test.ts`
- Modify: `src/AFK4.Player.Shell.Web/src/screens/ShopScreen.tsx`
- Modify: `src/AFK4.Player.Shell.Web/src/screens/ShopScreen.test.tsx`

**Interfaces:**
- Produces: `PlaceShopOrderRequest { lines: ShopOrderLineInput[]; idempotencyKey: string }`.
- Produces: `placeShopOrder(lines, idempotencyKey?)` that generates a key only when omitted.
- Adds `posSaleId: string | null` to the mirrored `ShopOrderDto`.

- [ ] **Step 1: Write failing API and gesture tests**

```typescript
it('posts a caller supplied shop order idempotency key', async () => {
  await api.placeShopOrder([{ productId: 'p1', quantity: 1 }], 'shop-gesture-1');
  expect(JSON.parse(fetchCalls[0].init.body as string)).toEqual({
    lines: [{ productId: 'p1', quantity: 1 }], idempotencyKey: 'shop-gesture-1'
  });
});

it('reuses one key while the same submit gesture is in flight', async () => {
  let resolveOrder!: (value: ShopOrderDto) => void;
  const pending = new Promise<ShopOrderDto>((resolve) => { resolveOrder = resolve; });
  const placeShopOrder = mock(async () => pending);
  render(<ShopScreen api={api({ placeShopOrder })}
    onNeedTopUp={() => {}} onDone={() => {}} pollIntervalMs={5000} />);
  await waitFor(() => screen.getByText('Cola'));
  fireEvent.click(screen.getByRole('button', { name: /добавить/i }));
  const submit = screen.getByRole('button', { name: /заказать/i });
  fireEvent.click(submit);
  fireEvent.click(submit);

  expect(placeShopOrder).toHaveBeenCalledTimes(1);
  expect(placeShopOrder.mock.calls[0][1]).toMatch(/^shop-/);
  resolveOrder({ id: 'o1', posSaleId: 's1', status: 'placed', lines: [],
    total: { currencyCode: 'TJS', minorUnits: 500 }, version: 1 } as ShopOrderDto);
  await waitFor(() => screen.getByText(/заказ принят/i));
});
```

- [ ] **Step 2: Run Bun tests and verify RED**

Run:

```powershell
Set-Location src/AFK4.Player.Shell.Web
bun test src/shellApi.test.ts src/screens/ShopScreen.test.tsx
```

Expected: body has no `idempotencyKey`, and the current method accepts only one argument.

- [ ] **Step 3: Implement one key per gesture**

```typescript
export interface PlaceShopOrderRequest {
  lines: ShopOrderLineInput[];
  idempotencyKey: string;
}

placeShopOrder: (lines: ShopOrderLineInput[], idempotencyKey = newKey()) =>
  call<ShopOrderDto>('/api/me/shop/orders', {
    method: 'POST',
    body: JSON.stringify({ lines, idempotencyKey })
  }),
```

In `ShopScreen`, guard synchronously with a `useRef(false)`, generate `shop-${crypto.randomUUID()}` once at the start of `placeOrder`, pass it to the API, and clear the guard in `finally`. A later explicit submit after a failed request generates a new key.

- [ ] **Step 4: Run GREEN verification and commit**

Run the Bun command from Step 2 and:

```powershell
bun run build
```

Expected: focused tests pass and production build exits 0.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Player.Shell.Web/src
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat(player-shell): make shop placement idempotent"
```

---

### Task 8: Concurrency, Reporting, And Final Verification

**Files:**
- Create: `tests/AFK4.Platform.Api.Tests/Shop/PostgresCommerceFactAttribute.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Shop/ShopCommercePostgresFixture.cs`
- Create: `tests/AFK4.Platform.Api.Tests/Shop/ShopCommercePostgresTests.cs`
- Modify: `src/AFK4.Shared.Contracts/Reports/SalesReportRowDto.cs`
- Modify: `src/AFK4.Shared.Contracts/Reports/SalesReportResultDto.cs`
- Modify: `src/AFK4.Platform.Api/Reports/EfReportService.cs`
- Modify: `src/AFK4.Platform.Api/Reports/ReportCsvExporter.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/ReportContractSerializationTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/OperatorShiftApiClientTests.cs`
- Modify: `tests/AFK4.Operator.App.Tests/ShiftWorkspaceViewModelTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfReportServiceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/ReportCsvExporterTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/BillingShiftIntegrationTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/ShopOrderProjectionTests.cs`
- Modify: `docs/progress/2026-05-12-vertical-slice-progress.md`
- Modify: `docs/superpowers/plans/README.md`

**Interfaces:**
- Verifies: one winner for the last tracked unit under PostgreSQL serializable isolation.
- Verifies: linked payment contributes to shift revenue once and refund subtracts it once.
- Verifies: Shop, POS, receipt, stock, and shift projections share the same identifiers and totals.

- [ ] **Step 1: Add the PostgreSQL concurrency proof**

```csharp
public sealed class PostgresCommerceFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "AFK4_COMMERCE_TEST_POSTGRES";

    public PostgresCommerceFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString) ||
            !new NpgsqlConnectionStringBuilder(connectionString).Database.EndsWith("_test", StringComparison.OrdinalIgnoreCase))
        {
            Skip = $"Set {EnvironmentVariable} to a PostgreSQL database whose name ends with _test.";
        }
    }
}

[PostgresCommerceFact]
public async Task ConcurrentPlacement_ForLastUnit_AllowsExactlyOnePaidOrder()
{
    var connectionString = Environment.GetEnvironmentVariable(
        PostgresCommerceFactAttribute.EnvironmentVariable)!;
    await using var database = await ShopCommercePostgresFixture.CreateAsync(connectionString);
    await database.SeedLastUnitScenarioAsync(stock: 1, walletMinorUnits: 10_000);

    var results = await Task.WhenAll(
        database.PlaceInIndependentScopeAsync("last-unit-a"),
        database.PlaceInIndependentScopeAsync("last-unit-b"));

    Assert.Single(results.Where(result => result.Succeeded));
    Assert.Single(results.Where(result => result.ErrorCode == "out_of_stock"));
    await using var verificationDb = database.CreateDbContext();
    Assert.Single(await verificationDb.ShopOrders.ToListAsync());
    Assert.Single(await verificationDb.PosSales.ToListAsync());
}
```

`ShopCommercePostgresFixture` must reject database names that do not end in `_test`, run `Database.MigrateAsync`, truncate only its test schema during disposal, and create an independent DI scope/`PlatformDbContext` per placement. The attribute skips only this PostgreSQL test when the explicit environment variable is absent; do not replace it with an InMemory concurrency claim.

- [ ] **Step 2: Add shift/report projection assertions**

Extend `BillingShiftIntegrationTests` to place a Shop order and assert current-shift payments/revenue include the positive wallet payment. Cancel it and assert the negative refund is included exactly once. Extend `ShopOrderProjectionTests` to assert `PosSaleId`, identical line retail totals, cost snapshots, and receipt link.

Append `GrossCostOfGoods`, `RefundedCostOfGoods`, and `NetCostOfGoods` money fields to both `SalesReportRowDto` and the corresponding totals in `SalesReportResultDto`. In `EfReportService`, compute them only from immutable line snapshots:

```csharp
var grossCostMinorUnits = sale.State is PosSaleStateNames.Paid or PosSaleStateNames.Refunded
    ? saleLines.Sum(line => checked((long)line.Quantity * line.UnitCostMinorUnits))
    : 0;
var refundedCostMinorUnits = sale.State == PosSaleStateNames.Refunded
    ? -grossCostMinorUnits
    : 0;
var netCostMinorUnits = grossCostMinorUnits + refundedCostMinorUnits;
```

Add the three row fields to sales CSV output as `gross_cogs_minor_units`, `refunded_cogs_minor_units`, and `net_cogs_minor_units`. Update every named DTO constructor in shared-contract and Operator tests. Assert that a paid linked order reports gross cost once and that its refund reduces net cost to zero while retail revenue/refund totals remain based on price/payment amounts.

- [ ] **Step 3: Run the affected backend suites**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "Shop|Pos|Inventory|BillingShiftIntegrationTests"
```

Expected: all selected tests pass; PostgreSQL concurrency test either passes or reports the repository's explicit environment skip.

- [ ] **Step 4: Run full cross-contract verification**

Because schema, money boundaries, and shared contracts changed, run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.slnx --no-restore
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.slnx --no-build
Set-Location src/AFK4.Player.Shell.Web
bun test
bun run build
Set-Location ../../..
& 'C:\Program Files\Git\cmd\git.exe' diff --check
```

Expected: build reports 0 errors, all non-environment-gated tests pass, Player Shell tests/build exit 0, and `git diff --check` prints nothing.

- [ ] **Step 5: Update durable progress and plan navigation**

In the compact progress snapshot, add one implemented bullet stating that Player Shop orders now settle as linked POS sales with wallet/receipt/shift/inventory integrity, and record the exact final verification commands/results. In `docs/superpowers/plans/README.md`, keep this plan active until merged; do not archive it on the topic branch.

- [ ] **Step 6: Self-review and final commit**

Inspect:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' status --short --branch
& 'C:\Program Files\Git\cmd\git.exe' diff --stat
& 'C:\Program Files\Git\cmd\git.exe' diff --check
```

Confirm there are no direct Shop-created `LedgerEntryEntity`, `PaymentEntity`, `ReceiptEntity`, or `StockMovementEntity` records in the new-order path; no retail-price-as-cost assignment; no notification before commit; and no unrelated user changes.

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add tests/AFK4.Platform.Api.Tests docs/progress/2026-05-12-vertical-slice-progress.md docs/superpowers/plans/README.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "test(commerce): verify shop financial integrity end to end"
```

Do not push, merge, or archive the plan without explicit user authorization.
