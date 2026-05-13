# AFK4 Phase 6 POS, Inventory, Shifts, And Receipts Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the backend-authoritative Phase 6 foundation for operator shifts, cash movements, POS catalog, stock movements, POS sales, returns, receipts, and mock/manual payment providers.

**Architecture:** Keep AFK4 as the existing ASP.NET Core modular monolith with PostgreSQL as source of truth. Add Shift, POS, Inventory, Receipt, and Payment provider application services with explicit boundaries; POS sales and stock movements are not merged into the billing ledger, while future money-changing ledger entries are linked to an open shift for reconciliation. Operator production UX remains a later Phase 7 concern.

**Tech Stack:** .NET 10, ASP.NET Core Minimal APIs, EF Core/Npgsql, EF Core InMemory tests, xUnit, shared DTO contracts in `AFK4.Shared.Contracts`.

---

## Scope

Phase 6 from the PRD covers:

- open and close operator shifts;
- shift cash movements and close reconciliation;
- product categories and products;
- stock movements with auditable inventory projection;
- POS sale states: `draft`, `pending_payment`, `paid`, `refunded`, `voided`;
- manual/mock payment provider abstraction for MVP;
- receipt records for paid and refunded POS sales;
- idempotency for critical POS, cash, payment, refund, void, and shift commands;
- audit records for protected shift, POS, inventory, and receipt actions.

This plan intentionally does not add:

- Operator App production UX, POS screens, hotkeys, role-aware navigation, or player search;
- fiscal printer, tax authority, country-specific receipt integrations, card acquirer integrations, or online payment gateways;
- reports beyond the close-shift summary needed to prove reconciliation data;
- Agent enforcement changes, Player Shell UI, web admin, local server, microservices, or non-Windows agents.

## Current Baseline

Already available and reused:

- staff sign-in, refresh-token rotation, branch-scoped authorization, and audit writer;
- organization and branch persistence;
- Phase 5 player accounts, immutable billing ledger, billing idempotency, tariffs, packages, and session billing;
- session lifecycle, device commands, floor map, and PostgreSQL runbook;
- EF Core/Npgsql migrations and EF InMemory API test host.

Important existing constraints:

- PostgreSQL remains source of truth.
- Ledger entries remain immutable; adding nullable `ShiftId` to future ledger entries is allowed, but no mutable balance fields may be introduced.
- POS sales are explicit POS records. Gameplay charges remain ledger entries. Stock movement records are append-only inventory history.

## File Structure

Create and modify these files:

```text
D:\afk4.net\
  docs\operations\local-postgres-smoke.md
  docs\progress\2026-05-12-vertical-slice-progress.md
  docs\superpowers\plans\2026-05-13-afk4-phase6-pos-inventory-shifts-receipts.md
  README.md
  src\AFK4.Shared.Contracts\
    Identity\StaffPermissionNames.cs
    Inventory\CreateStockMovementRequest.cs
    Inventory\InventoryStockDto.cs
    Inventory\StockMovementDto.cs
    Inventory\StockMovementTypeNames.cs
    Payments\ManualPaymentRequest.cs
    Payments\PaymentMethodNames.cs
    Pos\CreateProductCategoryRequest.cs
    Pos\CreateProductRequest.cs
    Pos\CreatePosSaleRequest.cs
    Pos\PosProductCategoryDto.cs
    Pos\PosProductDto.cs
    Pos\PosSaleDto.cs
    Pos\PosSaleLineDto.cs
    Pos\PosSaleStateNames.cs
    Pos\RefundPosSaleRequest.cs
    Pos\VoidPosSaleRequest.cs
    Receipts\ReceiptDto.cs
    Shifts\CashMovementDto.cs
    Shifts\CashMovementTypeNames.cs
    Shifts\CloseShiftRequest.cs
    Shifts\OpenShiftRequest.cs
    Shifts\RecordCashMovementRequest.cs
    Shifts\ShiftDto.cs
    Shifts\ShiftStateNames.cs
    Shifts\ShiftSummaryDto.cs
  src\AFK4.Platform.Api\
    Audit\AuditActionNames.cs
    Billing\BillingEntryFactory.cs
    Billing\EfBillingCommandService.cs
    Billing\EfPackageService.cs
    Billing\SessionBillingService.cs
    Data\CashMovementEntity.cs
    Data\LedgerEntryEntity.cs
    Data\PaymentEntity.cs
    Data\PosProductCategoryEntity.cs
    Data\PosProductEntity.cs
    Data\PosSaleEntity.cs
    Data\PosSaleLineEntity.cs
    Data\ReceiptEntity.cs
    Data\ShiftEntity.cs
    Data\StockMovementEntity.cs
    Data\PlatformDbContext.cs
    Data\Migrations\<timestamp>_AddPosInventoryShiftsReceipts.cs
    Identity\PermissionCatalog.cs
    Inventory\EfInventoryService.cs
    Inventory\IInventoryService.cs
    Payments\IPaymentProvider.cs
    Payments\ManualPaymentProvider.cs
    Pos\EfPosService.cs
    Pos\IPosService.cs
    Receipts\IReceiptNumberGenerator.cs
    Receipts\ReceiptNumberGenerator.cs
    Shifts\EfShiftService.cs
    Shifts\IOpenShiftResolver.cs
    Shifts\IShiftService.cs
    Program.cs
  tests\AFK4.Shared.Contracts.Tests\
    InventoryContractSerializationTests.cs
    PaymentContractSerializationTests.cs
    PosContractSerializationTests.cs
    ReceiptContractSerializationTests.cs
    ShiftContractSerializationTests.cs
  tests\AFK4.Platform.Api.Tests\
    BillingShiftIntegrationTests.cs
    EfInventoryServiceTests.cs
    EfPosServiceTests.cs
    EfShiftServiceTests.cs
    PosEndpointTests.cs
    ReceiptNumberGeneratorTests.cs
```

Responsibilities:

- `AFK4.Shared.Contracts.Shifts`: transport DTOs and constants for shift lifecycle, cash movements, and close summary.
- `AFK4.Shared.Contracts.Pos`: transport DTOs and constants for product catalog and sale lifecycle.
- `AFK4.Shared.Contracts.Inventory`: stock movement commands and inventory projections.
- `AFK4.Shared.Contracts.Payments`: manual/mock payment commands and method names.
- `AFK4.Shared.Contracts.Receipts`: receipt projection DTOs.
- `AFK4.Platform.Api.Shifts`: open shift lifecycle, one active shift per branch, cash movement writes, close reconciliation, and current-shift resolution.
- `AFK4.Platform.Api.Inventory`: append-only stock movements and derived stock-on-hand projection.
- `AFK4.Platform.Api.Pos`: sale state transitions, stock validation, payment/refund/void orchestration, and idempotency.
- `AFK4.Platform.Api.Payments`: MVP manual provider abstraction; no external gateway calls.
- `AFK4.Platform.Api.Receipts`: deterministic receipt numbering and receipt entity creation.

## Domain Rules

Shift rules:

```text
Only one open shift may exist per organization/branch.
Opening a shift requires starting cash amount >= 0 and an idempotency key.
Cash movements require an open shift, amount > 0, movement type, reason, and idempotency key.
Closing a shift requires an open shift, counted cash amount >= 0, and an idempotency key.
Closed shifts cannot accept new POS sales or cash movements.
Close summary includes starting cash, cash movements, POS cash payments, POS refunds, manual billing corrections already linked to the shift, expected cash, counted cash, and difference.
```

Inventory rules:

```text
Stock movement rows are append-only.
Stock on hand is SUM(QuantityDelta) grouped by ProductId.
Product price changes affect future sales only; sale lines store unit price and product name snapshots.
TrackStock products cannot be paid if the paid sale would make stock negative.
Non-stock products skip stock validation and stock movement writes.
Sale payment creates negative stock movements.
Sale refund creates positive stock movements for refunded quantities.
```

POS rules:

```text
draft -> pending_payment -> paid
draft -> voided
pending_payment -> voided
paid -> refunded
paid sales cannot be edited.
refunded sales cannot be paid or refunded again.
voided sales cannot be paid or refunded.
Every sale, payment, refund, void, cash movement, shift open, and shift close command uses idempotency.
Same idempotency key + same request returns the stored response.
Same idempotency key + different request returns 409 Conflict.
```

Receipt/payment rules:

```text
Manual payment provider supports cash and card_manual methods.
Manual provider accepts only positive payment amounts equal to the sale total for the first foundation.
Payment records are append-only.
Paid POS sale creates one receipt record.
Refunded POS sale creates one refund receipt record linked to the original sale.
Receipt numbers are unique per organization/branch and preserve historical sale totals.
```

Permissions:

```text
shifts.open
shifts.close
shifts.view
shifts.cash.manage
pos.catalog.manage
pos.sales.create
pos.sales.pay
pos.sales.refund
pos.sales.void
inventory.stock.manage
inventory.view
receipts.view
```

## Task 1: Shared Phase 6 Contracts And Permissions

**Files:**

- Create: `src\AFK4.Shared.Contracts\Shifts\ShiftStateNames.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\CashMovementTypeNames.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\OpenShiftRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\CloseShiftRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\RecordCashMovementRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\ShiftDto.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\CashMovementDto.cs`
- Create: `src\AFK4.Shared.Contracts\Shifts\ShiftSummaryDto.cs`
- Create: `src\AFK4.Shared.Contracts\Inventory\StockMovementTypeNames.cs`
- Create: `src\AFK4.Shared.Contracts\Inventory\CreateStockMovementRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Inventory\StockMovementDto.cs`
- Create: `src\AFK4.Shared.Contracts\Inventory\InventoryStockDto.cs`
- Create: `src\AFK4.Shared.Contracts\Payments\PaymentMethodNames.cs`
- Create: `src\AFK4.Shared.Contracts\Payments\ManualPaymentRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\PosSaleStateNames.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\CreateProductCategoryRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\CreateProductRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\CreatePosSaleRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\PosProductCategoryDto.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\PosProductDto.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\PosSaleLineDto.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\PosSaleDto.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\RefundPosSaleRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Pos\VoidPosSaleRequest.cs`
- Create: `src\AFK4.Shared.Contracts\Receipts\ReceiptDto.cs`
- Modify: `src\AFK4.Shared.Contracts\Identity\StaffPermissionNames.cs`
- Create tests:
  - `tests\AFK4.Shared.Contracts.Tests\ShiftContractSerializationTests.cs`
  - `tests\AFK4.Shared.Contracts.Tests\InventoryContractSerializationTests.cs`
  - `tests\AFK4.Shared.Contracts.Tests\PaymentContractSerializationTests.cs`
  - `tests\AFK4.Shared.Contracts.Tests\PosContractSerializationTests.cs`
  - `tests\AFK4.Shared.Contracts.Tests\ReceiptContractSerializationTests.cs`

- [ ] **Step 1: Write failing shift contract tests**

Create `tests\AFK4.Shared.Contracts.Tests\ShiftContractSerializationTests.cs`:

```csharp
using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ShiftContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void OpenShiftRequest_RoundTrips()
    {
        var request = new OpenShiftRequest(
            Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001"),
            new MoneyDto("TJS", 50000),
            "front register",
            "shift-open-001");

        var copy = JsonSerializer.Deserialize<OpenShiftRequest>(
            JsonSerializer.Serialize(request, Options),
            Options);

        Assert.Equal(request, copy);
    }

    [Fact]
    public void ShiftSummaryDto_RoundTrips()
    {
        var summary = new ShiftSummaryDto(
            Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001"),
            new MoneyDto("TJS", 50000),
            new MoneyDto("TJS", 25000),
            new MoneyDto("TJS", 100000),
            new MoneyDto("TJS", 10000),
            new MoneyDto("TJS", 165000),
            new MoneyDto("TJS", 164000),
            new MoneyDto("TJS", -1000));

        var copy = JsonSerializer.Deserialize<ShiftSummaryDto>(
            JsonSerializer.Serialize(summary, Options),
            Options);

        Assert.Equal(summary, copy);
    }

    [Fact]
    public void Constants_ExposeStableShiftNames()
    {
        Assert.Equal("open", ShiftStateNames.Open);
        Assert.Equal("closed", ShiftStateNames.Closed);
        Assert.Equal("cash_in", CashMovementTypeNames.CashIn);
        Assert.Equal("cash_out", CashMovementTypeNames.CashOut);
    }
}
```

- [ ] **Step 2: Write failing POS/inventory/payment/receipt contract tests**

Create the remaining contract tests with these required assertions:

```csharp
Assert.Equal("draft", PosSaleStateNames.Draft);
Assert.Equal("pending_payment", PosSaleStateNames.PendingPayment);
Assert.Equal("paid", PosSaleStateNames.Paid);
Assert.Equal("refunded", PosSaleStateNames.Refunded);
Assert.Equal("voided", PosSaleStateNames.Voided);
Assert.Equal("cash", PaymentMethodNames.Cash);
Assert.Equal("card_manual", PaymentMethodNames.CardManual);
Assert.Equal("purchase", StockMovementTypeNames.Purchase);
Assert.Equal("sale", StockMovementTypeNames.Sale);
Assert.Equal("refund", StockMovementTypeNames.Refund);
Assert.Equal("adjustment", StockMovementTypeNames.Adjustment);
```

Also round-trip:

```csharp
new CreateProductRequest(organizationId, categoryId, "Cola 0.5", "COLA-05", new MoneyDto("TJS", 1200), trackStock: true, allowNegativeStock: false, "product-001");
new CreateStockMovementRequest(organizationId, productId, StockMovementTypeNames.Purchase, 24, new MoneyDto("TJS", 900), "initial stock", "stock-001");
new CreatePosSaleRequest(organizationId, shiftId, new[] { new PosSaleLineDto(productId, "Cola 0.5", 2, new MoneyDto("TJS", 1200), new MoneyDto("TJS", 2400)) }, "sale-001");
new ManualPaymentRequest(organizationId, PaymentMethodNames.Cash, new MoneyDto("TJS", 2400), "cash drawer", "pay-001");
new ReceiptDto(receiptId, organizationId, branchId, saleId, "POS-20260513-0001", "sale", new MoneyDto("TJS", 2400), createdAtUtc);
```

- [ ] **Step 3: Run contract tests and verify RED**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "ShiftContractSerializationTests|InventoryContractSerializationTests|PaymentContractSerializationTests|PosContractSerializationTests|ReceiptContractSerializationTests" --no-restore -p:UseSharedCompilation=false
```

Expected: compile fails because Phase 6 contract namespaces and types do not exist.

- [ ] **Step 4: Implement contracts and permissions**

Create records exactly matching the test shapes. Add these constants to `StaffPermissionNames`:

```csharp
public const string OpenShift = "shifts.open";
public const string CloseShift = "shifts.close";
public const string ViewShift = "shifts.view";
public const string ManageShiftCash = "shifts.cash.manage";
public const string ManagePosCatalog = "pos.catalog.manage";
public const string CreatePosSale = "pos.sales.create";
public const string PayPosSale = "pos.sales.pay";
public const string RefundPosSale = "pos.sales.refund";
public const string VoidPosSale = "pos.sales.void";
public const string ManageInventoryStock = "inventory.stock.manage";
public const string ViewInventory = "inventory.view";
public const string ViewReceipt = "receipts.view";
```

- [ ] **Step 5: Run contract tests and commit**

Run the same targeted contract test command. Expected: all new Phase 6 contract tests pass.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Shared.Contracts tests/AFK4.Shared.Contracts.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add pos shift inventory contracts"
```

## Task 2: Shift Persistence, Service, And Billing Shift Link

**Files:**

- Create: `src\AFK4.Platform.Api\Data\ShiftEntity.cs`
- Create: `src\AFK4.Platform.Api\Data\CashMovementEntity.cs`
- Modify: `src\AFK4.Platform.Api\Data\LedgerEntryEntity.cs`
- Modify: `src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `src\AFK4.Platform.Api\Shifts\IShiftService.cs`
- Create: `src\AFK4.Platform.Api\Shifts\IOpenShiftResolver.cs`
- Create: `src\AFK4.Platform.Api\Shifts\EfShiftService.cs`
- Modify:
  - `src\AFK4.Platform.Api\Billing\BillingEntryFactory.cs`
  - `src\AFK4.Platform.Api\Billing\EfBillingCommandService.cs`
  - `src\AFK4.Platform.Api\Billing\EfPackageService.cs`
  - `src\AFK4.Platform.Api\Billing\SessionBillingService.cs`
- Create tests:
  - `tests\AFK4.Platform.Api.Tests\EfShiftServiceTests.cs`
  - `tests\AFK4.Platform.Api.Tests\BillingShiftIntegrationTests.cs`

- [ ] **Step 1: Write failing shift service tests**

Cover these cases in `EfShiftServiceTests`:

```text
OpenShiftAsync creates one open shift per branch and returns ShiftDto.
OpenShiftAsync rejects a second open shift in the same branch.
OpenShiftAsync replays same idempotency key and request.
RecordCashMovementAsync appends cash_in and cash_out rows to the open shift.
CloseShiftAsync computes expected cash and closes the shift.
CloseShiftAsync rejects already closed shifts.
```

Use EF InMemory setup matching existing `EfBillingCommandServiceTests` patterns.

- [ ] **Step 2: Write failing billing shift integration tests**

Cover these cases:

```text
TopUpWalletAsync requires an open shift after Phase 6.
TopUpWalletAsync writes ShiftId on the created ledger entry.
PurchasePackageAsync writes ShiftId on package purchase ledger entries.
Session prepaid wallet billing writes ShiftId on gameplay charge ledger entries.
```

Expected RED: `ShiftEntity`, `CashMovementEntity`, `ShiftId`, and shift resolver do not exist.

- [ ] **Step 3: Implement shift entities and DbContext mapping**

Add:

```csharp
public sealed class ShiftEntity
{
    public Guid ShiftId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid OpenedByStaffUserId { get; set; }
    public Guid? ClosedByStaffUserId { get; set; }
    public string State { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public long StartingCashMinorUnits { get; set; }
    public long CountedCashMinorUnits { get; set; }
    public long ExpectedCashMinorUnits { get; set; }
    public long DifferenceMinorUnits { get; set; }
    public string OpeningNote { get; set; } = string.Empty;
    public string ClosingNote { get; set; } = string.Empty;
    public DateTimeOffset OpenedAtUtc { get; set; }
    public DateTimeOffset? ClosedAtUtc { get; set; }
}
```

```csharp
public sealed class CashMovementEntity
{
    public Guid CashMovementId { get; set; }
    public Guid OrganizationId { get; set; }
    public Guid BranchId { get; set; }
    public Guid ShiftId { get; set; }
    public Guid CreatedByStaffUserId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public long AmountMinorUnits { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; set; }
}
```

Add nullable `ShiftId` to `LedgerEntryEntity`. Configure indexes:

```text
shifts: OrganizationId, BranchId, State
cash_movements: ShiftId, CreatedAtUtc
ledger_entries: ShiftId, CreatedAtUtc
```

- [ ] **Step 4: Implement `EfShiftService` and `IOpenShiftResolver`**

`IOpenShiftResolver.GetOpenShiftIdAsync(organizationId, branchId, cancellationToken)` returns the open shift id or a validation error. `EfShiftService` must use the existing billing command idempotency table or a small shared command-idempotency helper; do not create a second idempotency table for shifts in this task.

- [ ] **Step 5: Thread shift id into future ledger entries**

Update Phase 5 money writes so money-changing ledger entries require an open shift and set `LedgerEntryEntity.ShiftId`:

```text
wallet top-up
refund
manual correction
debt payment
package purchase
session prepaid wallet charge
session postpaid debt
session package/bonus consumption
```

Do not require an open shift for player account creation, tariff definition changes, package definition creation, or read-only projections.

- [ ] **Step 6: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfShiftServiceTests|BillingShiftIntegrationTests|EfBillingCommandServiceTests|EfPackageServiceTests|EfSessionBillingIntegrationTests" --no-restore -p:UseSharedCompilation=false
```

Expected: targeted tests pass after updating existing Phase 5 tests to seed an open shift where money-changing commands run.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add shift foundation and ledger shift links"
```

## Task 3: Product Catalog And Inventory Foundation

**Files:**

- Create:
  - `src\AFK4.Platform.Api\Data\PosProductCategoryEntity.cs`
  - `src\AFK4.Platform.Api\Data\PosProductEntity.cs`
  - `src\AFK4.Platform.Api\Data\StockMovementEntity.cs`
  - `src\AFK4.Platform.Api\Inventory\IInventoryService.cs`
  - `src\AFK4.Platform.Api\Inventory\EfInventoryService.cs`
- Modify: `src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
- Create: `tests\AFK4.Platform.Api.Tests\EfInventoryServiceTests.cs`

- [ ] **Step 1: Write failing inventory service tests**

Cover:

```text
Create category with unique branch/name.
Create product with category, SKU, current price, TrackStock, AllowNegativeStock.
Create purchase stock movement increases derived stock.
Create adjustment stock movement can increase or decrease stock.
Stock movement rejects quantity delta 0.
Tracked product rejects stock movement for wrong branch.
Catalog read returns active products with derived stock on hand.
```

- [ ] **Step 2: Implement catalog and stock entities**

Use these table names:

```text
pos_product_categories
pos_products
stock_movements
```

Required indexes:

```text
pos_product_categories: unique OrganizationId, BranchId, Name
pos_products: unique OrganizationId, BranchId, Sku
pos_products: OrganizationId, BranchId, CategoryId
stock_movements: ProductId, CreatedAtUtc
stock_movements: OrganizationId, BranchId, CreatedAtUtc
```

- [ ] **Step 3: Implement inventory service**

`EfInventoryService` must:

```text
create categories and products with idempotency;
append stock movement rows;
derive stock on hand from stock_movements;
reject duplicate category names and SKUs in a branch;
reject stock movement for inactive/missing products;
reject non-stock product stock movements.
```

- [ ] **Step 4: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter EfInventoryServiceTests --no-restore -p:UseSharedCompilation=false
```

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add pos catalog inventory service"
```

## Task 4: POS Sales, Manual Payments, Refunds, Voids, And Receipts

**Files:**

- Create:
  - `src\AFK4.Platform.Api\Data\PosSaleEntity.cs`
  - `src\AFK4.Platform.Api\Data\PosSaleLineEntity.cs`
  - `src\AFK4.Platform.Api\Data\PaymentEntity.cs`
  - `src\AFK4.Platform.Api\Data\ReceiptEntity.cs`
  - `src\AFK4.Platform.Api\Payments\IPaymentProvider.cs`
  - `src\AFK4.Platform.Api\Payments\ManualPaymentProvider.cs`
  - `src\AFK4.Platform.Api\Pos\IPosService.cs`
  - `src\AFK4.Platform.Api\Pos\EfPosService.cs`
  - `src\AFK4.Platform.Api\Receipts\IReceiptNumberGenerator.cs`
  - `src\AFK4.Platform.Api\Receipts\ReceiptNumberGenerator.cs`
- Modify:
  - `src\AFK4.Platform.Api\Data\PlatformDbContext.cs`
  - `src\AFK4.Platform.Api\Inventory\EfInventoryService.cs`
- Create tests:
  - `tests\AFK4.Platform.Api.Tests\EfPosServiceTests.cs`
  - `tests\AFK4.Platform.Api.Tests\ReceiptNumberGeneratorTests.cs`

- [ ] **Step 1: Write failing POS service tests**

Cover:

```text
Create sale requires an open shift.
Create sale snapshots product name, quantity, unit price, line total, and sale total.
Create sale rejects missing product and quantity <= 0.
Pay sale through manual cash provider moves state draft -> paid.
Pay sale creates payment record, receipt record, and negative sale stock movement.
Pay sale rejects insufficient stock for tracked products.
Pay sale is idempotent.
Refund paid sale moves state paid -> refunded and writes positive stock movement plus refund receipt.
Void draft sale moves state draft -> voided and writes no stock movement.
Void paid sale is rejected.
```

- [ ] **Step 2: Implement POS and receipt entities**

Use these table names:

```text
pos_sales
pos_sale_lines
payments
receipts
```

Required indexes:

```text
pos_sales: OrganizationId, BranchId, ShiftId, CreatedAtUtc
pos_sales: State
pos_sale_lines: PosSaleId
payments: PosSaleId, CreatedAtUtc
receipts: unique OrganizationId, BranchId, ReceiptNumber
receipts: PosSaleId
```

- [ ] **Step 3: Implement manual payment provider and receipt numbering**

`ManualPaymentProvider` accepts only:

```text
cash
card_manual
```

For the foundation, accepted payment amount must exactly equal sale total. `ReceiptNumberGenerator` creates branch-unique numbers:

```text
POS-YYYYMMDD-0001
REF-YYYYMMDD-0001
```

Generate numbers inside the same transaction as payment/refund.

- [ ] **Step 4: Implement `EfPosService`**

`EfPosService` must orchestrate:

```text
CreateSaleAsync
PaySaleAsync
RefundSaleAsync
VoidSaleAsync
GetSaleAsync
```

All state transitions run in transactions. Sale payment writes stock movements after stock validation and before committing the paid state. Refund writes stock movements and refund receipt in one transaction.

- [ ] **Step 5: Run tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfPosServiceTests|ReceiptNumberGeneratorTests|EfInventoryServiceTests|EfShiftServiceTests" --no-restore -p:UseSharedCompilation=false
```

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add pos sale payment receipt service"
```

## Task 5: Protected Phase 6 Endpoints, Permissions, And Audit

**Files:**

- Modify:
  - `src\AFK4.Platform.Api\Audit\AuditActionNames.cs`
  - `src\AFK4.Platform.Api\Identity\PermissionCatalog.cs`
  - `src\AFK4.Platform.Api\Program.cs`
- Create: `tests\AFK4.Platform.Api.Tests\PosEndpointTests.cs`

- [ ] **Step 1: Write failing endpoint tests**

Cover unauthorized, forbidden, and success paths for:

```text
POST /api/branches/{branchId}/shifts/open
GET /api/branches/{branchId}/shifts/current
POST /api/shifts/{shiftId}/cash-movements
POST /api/shifts/{shiftId}/close
POST /api/branches/{branchId}/pos/categories
POST /api/branches/{branchId}/pos/products
GET /api/branches/{branchId}/pos/catalog
POST /api/branches/{branchId}/inventory/stock-movements
POST /api/branches/{branchId}/pos/sales
POST /api/pos/sales/{saleId}/payments/manual
POST /api/pos/sales/{saleId}/refunds
POST /api/pos/sales/{saleId}/void
GET /api/pos/sales/{saleId}
GET /api/receipts/{receiptId}
```

Endpoint tests must assert cross-branch targets are hidden or forbidden consistently with existing Billing endpoint patterns.

- [ ] **Step 2: Add audit action names**

Add:

```csharp
public const string OpenShift = "shifts.open";
public const string CloseShift = "shifts.close";
public const string RecordCashMovement = "shifts.cash_movement";
public const string CreateProductCategory = "pos.categories.create";
public const string CreateProduct = "pos.products.create";
public const string CreateStockMovement = "inventory.stock.create";
public const string CreatePosSale = "pos.sales.create";
public const string PayPosSale = "pos.sales.pay";
public const string RefundPosSale = "pos.sales.refund";
public const string VoidPosSale = "pos.sales.void";
```

- [ ] **Step 3: Update role permissions**

Grant:

```text
owner, branch_manager: all Phase 6 permissions
shift_supervisor: shifts.open, shifts.close, shifts.view, shifts.cash.manage, pos.sales.create, pos.sales.pay, pos.sales.refund, pos.sales.void, inventory.view, receipts.view
cashier: shifts.open, shifts.view, pos.sales.create, pos.sales.pay, receipts.view
technician: inventory.view only if needed for device troubleshooting
accountant/auditor: shifts.view, inventory.view, receipts.view
```

- [ ] **Step 4: Register services and map endpoints**

Register services:

```csharp
builder.Services.AddScoped<IShiftService, EfShiftService>();
builder.Services.AddScoped<IOpenShiftResolver, EfShiftService>();
builder.Services.AddScoped<IInventoryService, EfInventoryService>();
builder.Services.AddScoped<IPosService, EfPosService>();
builder.Services.AddScoped<IPaymentProvider, ManualPaymentProvider>();
builder.Services.AddScoped<IReceiptNumberGenerator, ReceiptNumberGenerator>();
```

Map endpoints in `Program.cs` following existing authorization/audit patterns: authenticate first, authorize branch-scoped permission second, validate route/body organization and branch third, call service fourth, write audit for allowed/denied privileged attempts.

- [ ] **Step 5: Run endpoint tests and commit**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter PosEndpointTests --no-restore -p:UseSharedCompilation=false
```

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
& 'C:\Program Files\Git\cmd\git.exe' commit -m "feat: add protected pos shift endpoints"
```

## Task 6: EF Migration, README, Runbook, And Progress

**Files:**

- Create: `src\AFK4.Platform.Api\Data\Migrations\<timestamp>_AddPosInventoryShiftsReceipts.cs`
- Modify: `src\AFK4.Platform.Api\Data\Migrations\PlatformDbContextModelSnapshot.cs`
- Modify: `README.md`
- Modify: `docs\operations\local-postgres-smoke.md`
- Modify: `docs\progress\2026-05-12-vertical-slice-progress.md`

- [ ] **Step 1: Create EF migration**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' ef migrations add AddPosInventoryShiftsReceipts --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
```

- [ ] **Step 2: Review migration**

Verify it creates:

```text
shifts
cash_movements
pos_product_categories
pos_products
stock_movements
pos_sales
pos_sale_lines
payments
receipts
```

Verify it adds `ShiftId` to `ledger_entries` and indexes it.

Verify it does not add:

```text
mutable stock balance columns
mutable cash balance columns
mutable wallet/debt balance columns
fiscal integration tables
external payment gateway tables
```

- [ ] **Step 3: Update local PostgreSQL smoke runbook**

Add Phase 6 smoke path:

```text
open shift with idempotency smoke-shift-open-001
create category and product
append purchase stock movement
read catalog and stock on hand
create POS sale with idempotency smoke-pos-sale-001
pay sale with manual cash idempotency smoke-pos-pay-001
inspect sale state paid, payment row, receipt row, stock movement sale row
refund sale with idempotency smoke-pos-refund-001
inspect sale state refunded, refund receipt row, refund stock movement row
record cash movement with idempotency smoke-cash-in-001
close shift with idempotency smoke-shift-close-001
inspect shifts, cash_movements, pos_sales, pos_sale_lines, payments, receipts, stock_movements, audit_records
```

- [ ] **Step 4: Update README and progress**

README endpoint list must include Phase 6 endpoints and note:

```text
POS sales are explicit sale records.
Stock on hand is derived from stock_movements.
Shift close summary reconciles starting cash, cash movements, POS payments/refunds, and shift-linked ledger entries.
Manual/mock payment provider is the only provider in Phase 6.
```

Progress doc must record implementation status, latest verification, known limitations, and whether Phase 6 live smoke has been run.

- [ ] **Step 5: Run docs and migration sanity checks, then commit**

Run:

```powershell
$phase6SanityPattern = @('T' + 'BD', 'FIX' + 'ME', 'mutable stock balance', 'wallet_balance', 'debt_balance') -join '|'
rg -n $phase6SanityPattern docs README.md src/AFK4.Platform.Api/Data/Migrations
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected: grep finds no incomplete markers in Phase 6 docs/migration; build succeeds with 0 warnings and 0 errors.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add README.md docs src/AFK4.Platform.Api/Data/Migrations
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: add phase 6 migration and runbook"
```

## Task 7: Final Verification

**Files:**

- Modify only if verification reveals a real defect in Phase 6 implementation.

- [ ] **Step 1: Run targeted Phase 6 tests**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter "ShiftContractSerializationTests|InventoryContractSerializationTests|PaymentContractSerializationTests|PosContractSerializationTests|ReceiptContractSerializationTests" --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "EfShiftServiceTests|BillingShiftIntegrationTests|EfInventoryServiceTests|EfPosServiceTests|ReceiptNumberGeneratorTests|PosEndpointTests" --no-restore -p:UseSharedCompilation=false
```

Expected: all targeted Phase 6 tests pass.

- [ ] **Step 2: Run full build and full test suite**

Run:

```powershell
& 'C:\Program Files\dotnet\dotnet.exe' build AFK4.sln --no-restore -p:UseSharedCompilation=false
& 'C:\Program Files\dotnet\dotnet.exe' test AFK4.sln --no-restore -p:UseSharedCompilation=false
```

Expected:

```text
build succeeded, 0 warnings, 0 errors
all tests passed
```

- [ ] **Step 3: Self-review scope**

Verify:

```text
No Operator production UX was added.
No fiscal/payment gateway integration was added.
No mutable balance fields were added.
POS sale records are separate from billing ledger records.
Stock projection is derived from stock_movements.
Money-changing future ledger entries are shift-linked.
Critical POS/shift commands are idempotent.
Protected endpoints audit allowed and denied attempts.
```

- [ ] **Step 4: Record verification and commit**

Update `docs\progress\2026-05-12-vertical-slice-progress.md` with final Phase 6 verification evidence.

Commit:

```powershell
& 'C:\Program Files\Git\cmd\git.exe' add docs/progress/2026-05-12-vertical-slice-progress.md
& 'C:\Program Files\Git\cmd\git.exe' commit -m "docs: record phase 6 verification"
```

## Self-Review

- Scope matches Phase 6 from the PRD: product catalog, stock, sales, returns, shift open/close, cash reconciliation, receipts, and manual/mock payment provider foundation.
- Scope intentionally excludes Operator production UX, fiscal integrations, payment gateways, reports beyond close summary, Agent enforcement, Player Shell UI, web admin, local server, and microservices.
- The plan preserves Phase 5 immutable ledger decisions and adds only nullable `ShiftId` linkage for future reconciliation.
- POS sales, inventory movements, payments, and receipts are explicit records with append-only history where money or stock changes.
- No mutable wallet, debt, package, stock, or cash balance fields are introduced; wallet/debt remain ledger projections and stock is derived from `stock_movements`.
- Every critical POS, shift, cash, payment, refund, void, and money-linked command has idempotency coverage.
