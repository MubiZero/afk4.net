# POS Split Settlement And Refunds Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Operator POS multipart payment and symmetric refund atomic, idempotent, wallet-aware, inventory-safe, receipt-backed, and shift-linked.

**Architecture:** Introduce a shared multipart settlement contract and an `IPosSettlementService` that owns one serializable unit of work. POS stages sale/payment/receipt mutations, Billing stages wallet ledger mutations, Inventory supplies cost/stock rules, and `PaymentEntity.LedgerEntryId` durably links wallet parts to their reversals.

**Tech Stack:** .NET 10, ASP.NET Core minimal APIs, EF Core, PostgreSQL 16, xUnit, React/TypeScript, Bun test.

## Global Constraints

- Execute after `2026-07-14-operator-commerce-ui-completion.md` on the same topic branch.
- Reuse `AFK4.Shared.Contracts.Sessions.PaymentPartDto`; do not introduce another part DTO.
- One command must commit or roll back wallet, payment rows, stock, receipt, and sale state together.
- Payment methods are unique and limited to `cash`, `card_manual`, and `wallet`.
- Wallet payment requires `PosSaleEntity.PlayerAccountId` and an authoritative Billing balance check.
- Cash change is a UI concern; persist only applied cash.
- Refund the immutable original payment mix, not a newly selected method.
- Preserve linked Player Shop order cancellation/refund semantics.

---

### Task 1: Add Multipart Settlement Contracts

**Files:**
- Create: `src/AFK4.Shared.Contracts/Pos/SettlePosSaleRequest.cs`
- Modify: `tests/AFK4.Shared.Contracts.Tests/ContractSerializationTests.cs`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.ts`
- Modify: `src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts`

**Interfaces:**
- Consumes: `PaymentPartDto`, `MoneyDto`.
- Produces: `SettlePosSaleRequest(Guid OrganizationId, IReadOnlyList<PaymentPartDto> Payments, string Note, string IdempotencyKey)` and `pos.settleSale(saleId, request)`.

- [ ] **Step 1: Write the failing contract serialization test**

```csharp
[Fact]
public void SettlePosSaleRequest_RoundTripsPaymentParts()
{
    var request = new SettlePosSaleRequest(
        TestIds.OrganizationId,
        [
            new PaymentPartDto("wallet", new MoneyDto("TJS", 4_000)),
            new PaymentPartDto("cash", new MoneyDto("TJS", 6_000))
        ],
        "operator POS checkout",
        "pos-settle-1");

    var copy = JsonSerializer.Deserialize<SettlePosSaleRequest>(JsonSerializer.Serialize(request))!;
    Assert.Equal(2, copy.Payments.Count);
    Assert.Equal(10_000, copy.Payments.Sum(part => part.Amount.MinorUnits));
}
```

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter 'FullyQualifiedName~SettlePosSaleRequest' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: compile failure because the request is absent.

- [ ] **Step 3: Add the contract and typed web client**

```csharp
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Pos;

public sealed record SettlePosSaleRequest(
    Guid OrganizationId,
    IReadOnlyList<PaymentPartDto> Payments,
    string Note,
    string IdempotencyKey);
```

Add TypeScript shape and client method:

```ts
export type SettlePosSaleRequest = {
  organizationId: string;
  payments: PaymentPartDto[];
  note: string;
  idempotencyKey: string;
};

settleSale: (saleId: string, request: SettlePosSaleRequest) =>
  api.post<PosSaleDto, SettlePosSaleRequest>(`/api/pos/sales/${saleId}/settlements`, request)
```

- [ ] **Step 4: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter 'FullyQualifiedName~SettlePosSaleRequest' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
cd src/AFK4.Operator.App.Web && bun test src/operatorApiClients.test.ts && cd ../../..
git add src/AFK4.Shared.Contracts/Pos/SettlePosSaleRequest.cs tests/AFK4.Shared.Contracts.Tests/ContractSerializationTests.cs src/AFK4.Operator.App.Web/src/operatorApiClients.ts src/AFK4.Operator.App.Web/src/operatorApiClients.test.ts
git diff --cached --check
git commit -m "feat(contracts): add multipart POS settlement"
```

### Task 2: Link Wallet Payment Rows To Ledger Entries

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PaymentEntity.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260714120000_LinkPaymentsToLedgerEntries.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/20260714120000_LinkPaymentsToLedgerEntries.Designer.cs`
- Modify: `src/AFK4.Platform.Api/Data/Migrations/PlatformDbContextModelSnapshot.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfShopPosSettlementService.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs`

**Interfaces:**
- Produces: `PaymentEntity.LedgerEntryId : Guid?`, unique for non-null IDs, null for cash/card.

- [ ] **Step 1: Write the failing linked-wallet test**

After creating a paid Player Shop sale, assert:

```csharp
var payment = await db.Payments.SingleAsync(p => p.PosSaleId == result.Sale!.PosSaleId && p.PaymentKind == "payment");
Assert.Equal(PaymentMethodNames.Wallet, payment.PaymentMethod);
Assert.Equal(result.WalletEntry!.LedgerEntryId, payment.LedgerEntryId);
```

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfShopPosSettlementServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: compile failure because `LedgerEntryId` is absent.

- [ ] **Step 3: Add model and mapping**

```csharp
public Guid? LedgerEntryId { get; set; }
```

Configure:

```csharp
entity.HasIndex(payment => payment.LedgerEntryId).IsUnique();
entity.HasOne<LedgerEntryEntity>()
    .WithMany()
    .HasForeignKey(payment => payment.LedgerEntryId)
    .OnDelete(DeleteBehavior.Restrict);
```

Set the ID on Player Shop wallet payment and refund rows.

- [ ] **Step 4: Generate migration and run GREEN**

```bash
dotnet ef migrations add LinkPaymentsToLedgerEntries --project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj --startup-project src/AFK4.Platform.Api/AFK4.Platform.Api.csproj
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfShopPosSettlementServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: migration contains nullable FK plus unique index; tests PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PaymentEntity.cs src/AFK4.Platform.Api/Data/PlatformDbContext.cs src/AFK4.Platform.Api/Data/Migrations src/AFK4.Platform.Api/Pos/EfShopPosSettlementService.cs tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs
git diff --cached --check
git commit -m "feat(pos): link wallet payments to ledger entries"
```

### Task 3: Implement Atomic POS Split Settlement

**Files:**
- Create: `src/AFK4.Platform.Api/Pos/IPosSettlementService.cs`
- Create: `src/AFK4.Platform.Api/Pos/EfPosSettlementService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfPosService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/IPosService.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs`
- Create: `tests/AFK4.Platform.Api.Tests/EfPosSettlementServiceTests.cs`

**Interfaces:**
- Produces: `IPosSettlementService.SettleAsync(Guid saleId, Guid actorStaffUserId, SettlePosSaleRequest request, CancellationToken)`.
- Consumes: `IWalletSettlementService`, `IInventoryCostService`, `IReceiptNumberGenerator`, `ILowStockNotifier`, `PlatformDbContext`.

- [ ] **Step 1: Write validation RED tests**

Cover empty parts, duplicate methods, unsupported method, non-positive amount, mixed currency, wrong total, wallet without player, insufficient wallet, and closed/mismatched shift. Representative assertion:

```csharp
var result = await service.SettleAsync(saleId, actorId, request with
{
    Payments = [
        new("cash", Money(5_000)),
        new("cash", Money(5_000))
    ]
}, default);
Assert.Equal("invalid_payment_split", result.Error);
Assert.Empty(db.Payments);
Assert.Empty(db.StockMovements);
```

- [ ] **Step 2: Write atomic success and rollback tests**

For wallet + cash, assert two payments, one wallet debit linked from the wallet payment, one receipt, tracked stock movement only, and paid sale. Inject an inventory or receipt failure and assert all six tables remain unchanged except the pre-existing draft sale/lines.

- [ ] **Step 3: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfPosSettlementServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: compile failure because the service is absent.

- [ ] **Step 4: Define the service boundary**

```csharp
public interface IPosSettlementService
{
    Task<BillingCommandServiceResult<PosSaleDto>> SettleAsync(
        Guid posSaleId,
        Guid actorStaffUserId,
        SettlePosSaleRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] **Step 5: Implement normalization and serializable transaction**

Normalize methods/currency, sort parts for the idempotency hash, validate exact total, and run the full mutation under `IsolationLevel.Serializable`. Create a wallet debit with:

```csharp
var debit = await walletSettlementService.DebitAsync(
    sale.OrganizationId,
    sale.BranchId,
    sale.PlayerAccountId.Value,
    sessionId: null,
    sale.ShiftId,
    walletPart.Amount.MinorUnits,
    sale.CurrencyCode,
    $"POS sale {sale.PosSaleId:D}",
    request.Note,
    actorStaffUserId,
    now,
    cancellationToken);
```

For each part create one `PaymentEntity`; set `LedgerEntryId` only on wallet. Reuse immutable `PosSaleLineEntity.TracksStock` and `UnitCostMinorUnits`. Commit before low-stock notification.

- [ ] **Step 6: Delegate single-payment compatibility and run GREEN**

`EfPosService.PaySaleAsync` converts `ManualPaymentRequest` to one `PaymentPartDto` and calls `SettleAsync`. Register the service in `Program.cs`.

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfPosSettlementServiceTests|FullyQualifiedName~EfPosServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Platform.Api/Pos src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/EfPosSettlementServiceTests.cs tests/AFK4.Platform.Api.Tests/EfPosServiceTests.cs
git diff --cached --check
git commit -m "feat(pos): settle split payments atomically"
```

### Task 4: Refund The Original Payment Mix

**Files:**
- Modify: `src/AFK4.Platform.Api/Pos/IPosSettlementService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfPosSettlementService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfPosService.cs`
- Modify: `src/AFK4.Platform.Api/Pos/EfShopPosSettlementService.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/EfPosSettlementServiceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs`

**Interfaces:**
- Produces: `RefundAsync(Guid posSaleId, Guid actorStaffUserId, RefundPosSaleRequest request, CancellationToken)`.

- [ ] **Step 1: Write failing mixed-refund tests**

Settle wallet 4,000 + cash 6,000, refund, then assert:

```csharp
Assert.Equal([-6_000L, -4_000L], refundPayments.OrderBy(p => p.AmountMinorUnits).Select(p => p.AmountMinorUnits));
Assert.Single(await db.LedgerEntries.Where(e => e.ReversesLedgerEntryId == walletDebitId).ToListAsync());
Assert.Single(await db.Receipts.Where(r => r.PosSaleId == saleId && r.ReceiptType == "refund").ToListAsync());
```

Repeat the same request and assert counts do not change.

- [ ] **Step 2: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfPosSettlementServiceTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
```

Expected: current refund writes one payment method and cannot reverse an ordinary POS wallet debit.

- [ ] **Step 3: Implement original-mix refund**

Load original `PaymentKind == "payment"` rows. For each wallet row, require `LedgerEntryId`, load the canonical debit, and call `ReverseAsync`. For each original row create a negative `PaymentKind == "refund"` row with the same method and amount magnitude. Create stock returns from immutable line snapshots and one refund receipt. Fail closed when original payment totals/currency/linkage do not match the sale.

- [ ] **Step 4: Keep linked Shop cancellation compatible**

Ensure `EfShopPosSettlementService` produces the same payment-ledger linkage so `RefundLinkedSaleAsync` and ordinary refund share invariants without double-reversing Shop orders.

- [ ] **Step 5: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~EfPosSettlementServiceTests|FullyQualifiedName~EfShopPosSettlementServiceTests|FullyQualifiedName~ShopCommerce' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
git add src/AFK4.Platform.Api/Pos tests/AFK4.Platform.Api.Tests/EfPosSettlementServiceTests.cs tests/AFK4.Platform.Api.Tests/Shop/EfShopPosSettlementServiceTests.cs
git diff --cached --check
git commit -m "fix(pos): refund immutable payment mix"
```

### Task 5: Expose Settlement Endpoint And Complete Operator POS

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/PosEndpointTests.cs`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/PaymentDialog.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/checkoutState.ts`
- Modify: `src/AFK4.Operator.App.Web/src/checkoutState.test.ts`

**Interfaces:**
- Produces: `POST /api/pos/sales/{saleId}/settlements`; Operator submits all `PaymentPartDto` values.

- [ ] **Step 1: Write endpoint RED tests**

Assert permission, organization scope, stable error body, multipart success, and audit detail with part count.

- [ ] **Step 2: Write UI RED tests**

Select a player with a 45 TJS wallet balance, open checkout, choose split, enter wallet 20 + cash 80 for a 100 total, and assert `settleSale` receives both parts. Assert failed settlement keeps cart, client, and dialog values.

- [ ] **Step 3: Run RED**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~PosEndpointTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
cd src/AFK4.Operator.App.Web && bun test src/BackendPosWorkspace.test.tsx src/checkoutState.test.ts && cd ../../..
```

- [ ] **Step 4: Map endpoint and stable errors**

Call `IPosSettlementService.SettleAsync`. Translate the spec codes to 400/409 without returning provider exception text. Keep the old manual endpoint as a one-part adapter.

- [ ] **Step 5: Submit the complete UI payment list**

Pass:

```tsx
walletBalanceMinorUnits={selectedPosPlayer?.balanceMinorUnits ?? null}
allowSplit={selectedPosPlayer !== null}
```

Replace `paySaleManual` with:

```ts
await clients.pos.settleSale(saleId, {
  organizationId: nextBackend.session.organizationId,
  payments,
  note: 'operator POS checkout',
  idempotencyKey: paymentAttemptKey
});
```

Keep one payment key for the gesture and ambiguous retry. Clear cart only after authoritative success.

- [ ] **Step 6: Prevent duplicate methods in PaymentDialog**

Filter options by methods used in other rows; disable Add when all available methods are present. Add a pure helper test proving uniqueness.

- [ ] **Step 7: Run GREEN and commit**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~PosEndpointTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
cd src/AFK4.Operator.App.Web && bun test src/BackendPosWorkspace.test.tsx src/checkoutState.test.ts && cd ../../..
git add src/AFK4.Platform.Api/Endpoints/PosEndpoints.cs tests/AFK4.Platform.Api.Tests/PosEndpointTests.cs src/AFK4.Operator.App.Web/src/BackendPosWorkspace.tsx src/AFK4.Operator.App.Web/src/BackendPosWorkspace.test.tsx src/AFK4.Operator.App.Web/src/PaymentDialog.tsx src/AFK4.Operator.App.Web/src/checkoutState.ts src/AFK4.Operator.App.Web/src/checkoutState.test.ts
git diff --cached --check
git commit -m "feat(operator-pos): accept mixed payment"
```

### Task 6: Prove Reports, Journal Refunds, And PostgreSQL Races

**Files:**
- Modify: `tests/AFK4.Platform.Api.Tests/EfReportServiceTests.cs`
- Modify: `tests/AFK4.Platform.Api.Tests/ReportCsvExporterTests.cs`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx`
- Create: `tests/AFK4.Platform.Api.Tests/Pos/PostgresPosSettlementConcurrencyTests.cs`
- Create or reuse: `tests/AFK4.Platform.Api.Tests/Pos/PosSettlementPostgresFixture.cs`

**Interfaces:**
- Consumes: payment rows by method/kind, cash journal refund client, PostgreSQL test connection convention.
- Produces: proof that split/refund reconciliation and concurrent mutation are correct.

- [ ] **Step 1: Add report and journal tests**

Assert a wallet+cash sale and refund net each method independently, cash expected amount changes only by the cash part, COGS is unchanged, and `CashReceiptsLedger` refreshes the refunded receipt after success. Extend `CashJournalWorkspace.test.tsx` to assert that operations, receipts, and permission-gated anti-fraud review remain reachable after the payment changes.

- [ ] **Step 2: Add deterministic PostgreSQL overlap tests**

Use an interceptor/barrier like `ShopCommercePostgresFixture`. Cover:

```csharp
[PostgresPosFact]
public async Task ConcurrentSettlement_CreatesOnePaidSaleAndOneEffectSet() { /* two scoped services, one barrier */ }

[PostgresPosFact]
public async Task ConcurrentMixedRefund_CreatesOneReversalSet() { /* two refund commands */ }
```

Assert exact persisted counts, not only returned status.

- [ ] **Step 3: Run focused proof**

```bash
AFK4_POS_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=afk4_pos_test;Username=postgres;Password=postgres' \
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter 'FullyQualifiedName~PostgresPosSettlementConcurrencyTests|FullyQualifiedName~EfReportServiceTests|FullyQualifiedName~ReportCsvExporterTests' -p:NuGetAudit=false -p:UseSharedCompilation=false -v minimal
cd src/AFK4.Operator.App.Web && bun test src/cash/CashReceiptsLedger.test.tsx src/cash/CashJournalWorkspace.test.tsx && cd ../../..
```

Expected: all tests PASS against an isolated database whose name ends in `_test`.

- [ ] **Step 4: Commit**

```bash
git add tests/AFK4.Platform.Api.Tests src/AFK4.Operator.App.Web/src/cash/CashReceiptsLedger.test.tsx src/AFK4.Operator.App.Web/src/cash/CashJournalWorkspace.test.tsx
git diff --cached --check
git commit -m "test(pos): prove split settlement and refund integrity"
```
