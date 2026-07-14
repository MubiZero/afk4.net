using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopPosSettlementServiceTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerAccountId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-13T12:00:00Z");

    [Fact]
    public async Task CreatePaidWalletSaleAsync_StagesCompleteShiftLinkedSale()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        var shift = await SeedOpenShiftAsync(db);
        var tracked = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 275);
        var serviceLine = await SeedProductAsync(db, "Table service", 350, trackStock: false, avgCostMinorUnits: 999);
        await SeedStockAsync(db, tracked.ProductId, quantity: 10, unitCostMinorUnits: 275);
        var service = CreateService(db);

        var result = await service.CreatePaidWalletSaleAsync(
            SaleRequest(
                new ShopOrderLineInput(tracked.ProductId, 2),
                new ShopOrderLineInput(serviceLine.ProductId, 1)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(PosSaleStateNames.Paid, result.Sale!.State);
        Assert.Equal(shift.ShiftId, result.Sale.ShiftId);
        Assert.Equal(2_750, result.Sale.TotalMinorUnits);
        Assert.Equal(PlayerAccountId, result.Sale.PlayerAccountId);
        Assert.Equal(SessionId, result.Sale.SessionId);
        Assert.Equal(Now, result.Sale.PaidAtUtc);

        var sale = AssertAddedSingle<PosSaleEntity>(db);
        var lines = Added<PosSaleLineEntity>(db);
        var payment = AssertAddedSingle<PaymentEntity>(db);
        var receipt = AssertAddedSingle<ReceiptEntity>(db);
        var movement = AssertAddedSingle<StockMovementEntity>(db);
        var debit = AssertAddedSingle<LedgerEntryEntity>(db);

        Assert.Same(sale, result.Sale);
        Assert.Equal(2, lines.Count);
        Assert.All(lines, line => Assert.Equal(sale.PosSaleId, line.PosSaleId));
        Assert.Equal(275, lines.Single(line => line.ProductId == tracked.ProductId).UnitCostMinorUnits);
        Assert.Equal(0, lines.Single(line => line.ProductId == serviceLine.ProductId).UnitCostMinorUnits);
        Assert.Equal("payment", payment.PaymentKind);
        Assert.Equal("wallet", payment.Provider);
        Assert.Equal(PaymentMethodNames.Wallet, payment.PaymentMethod);
        Assert.Equal(sale.PosSaleId, payment.PosSaleId);
        Assert.Equal(shift.ShiftId, payment.ShiftId);
        Assert.Equal(2_750, payment.AmountMinorUnits);
        Assert.Equal(debit.LedgerEntryId, payment.LedgerEntryId);
        Assert.Equal("sale", receipt.ReceiptType);
        Assert.Equal(sale.PosSaleId, receipt.PosSaleId);
        Assert.Equal(2_750, receipt.TotalMinorUnits);
        Assert.Equal(-2, movement.QuantityDelta);
        Assert.Equal(275, movement.UnitCostMinorUnits);
        Assert.Equal(StockMovementTypeNames.Sale, movement.MovementType);
        Assert.Equal(-2_750, debit.AmountMinorUnits);
        Assert.Equal(LedgerEntryTypeNames.WalletPayment, debit.EntryType);
        Assert.Same(debit, result.WalletEntry);
        Assert.Same(receipt, result.Receipt);
        Assert.Equal(0, await db.PosSales.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.Payments.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.Receipts.AsNoTracking().CountAsync());
    }

    [Theory]
    [InlineData(false, true, "open_shift_required")]
    [InlineData(true, false, "insufficient_funds")]
    public async Task CreatePaidWalletSaleAsync_Failure_StagesNoPartialFinance(
        bool hasOpenShift,
        bool hasFunds,
        string expectedCode)
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: hasFunds ? 10_000 : 100);
        if (hasOpenShift)
        {
            await SeedOpenShiftAsync(db);
        }

        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 275);
        await SeedStockAsync(db, product.ProductId, quantity: 10, unitCostMinorUnits: 275);
        var service = CreateService(db);

        var result = await service.CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expectedCode, result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_MultipleOpenShifts_ReturnsOpenShiftAmbiguous()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: false, avgCostMinorUnits: 0);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("open_shift_ambiguous", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_ShiftCurrencyMismatch_ReturnsMixedCurrency()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        var shift = await SeedOpenShiftAsync(db);
        shift.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: false, avgCostMinorUnits: 0);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("mixed_currency", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_StockQuantityOverflow_ReturnsStockQuantityInvalid()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 275);
        await SeedStockAsync(db, product.ProductId, quantity: int.MaxValue, unitCostMinorUnits: 275);
        await SeedStockAsync(db, product.ProductId, quantity: int.MaxValue, unitCostMinorUnits: 275);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 1)),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("stock_quantity_invalid", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_OutOfStock_ReturnsOutOfStock()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 275);
        await SeedStockAsync(db, product.ProductId, quantity: 1, unitCostMinorUnits: 275);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 2)),
            CancellationToken.None);

        Assert.Equal("out_of_stock", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_InactiveProduct_ReturnsProductUnavailable()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(
            db,
            "Hidden Cola",
            1_200,
            trackStock: true,
            avgCostMinorUnits: 275,
            isActive: false);
        await SeedStockAsync(db, product.ProductId, quantity: 10, unitCostMinorUnits: 275);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 1)),
            CancellationToken.None);

        Assert.Equal("product_unavailable", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_MixedCurrency_ReturnsMixedCurrency()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var somoni = await SeedProductAsync(db, "Cola", 1_200, trackStock: false, avgCostMinorUnits: 0);
        var dollars = await SeedProductAsync(db, "Imported snack", 500, trackStock: false, avgCostMinorUnits: 0, currencyCode: "USD");

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(
                new ShopOrderLineInput(somoni.ProductId, 1),
                new ShopOrderLineInput(dollars.ProductId, 1)),
            CancellationToken.None);

        Assert.Equal("mixed_currency", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_NonStockService_CreatesNoMovement()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Table service", 350, trackStock: false, avgCostMinorUnits: 999);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(new ShopOrderLineInput(product.ProductId, 3)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var line = AssertAddedSingle<PosSaleLineEntity>(db);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(1_050, line.LineTotalMinorUnits);
        Assert.Equal(0, line.UnitCostMinorUnits);
        Assert.Empty(Added<StockMovementEntity>(db));
        Assert.Single(Added<PosSaleEntity>(db));
        Assert.Single(Added<PaymentEntity>(db));
        Assert.Single(Added<ReceiptEntity>(db));
        Assert.Single(Added<LedgerEntryEntity>(db));
    }

    [Fact]
    public async Task CreatePaidWalletSaleAsync_DuplicateLines_AggregatesQuantityAndTotal()
    {
        await using var db = CreateDbContext();
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 275);
        await SeedStockAsync(db, product.ProductId, quantity: 10, unitCostMinorUnits: 275);

        var result = await CreateService(db).CreatePaidWalletSaleAsync(
            SaleRequest(
                new ShopOrderLineInput(product.ProductId, 2),
                new ShopOrderLineInput(product.ProductId, 3)),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var sale = AssertAddedSingle<PosSaleEntity>(db);
        var line = AssertAddedSingle<PosSaleLineEntity>(db);
        var movement = AssertAddedSingle<StockMovementEntity>(db);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(6_000, line.LineTotalMinorUnits);
        Assert.Equal(6_000, sale.TotalMinorUnits);
        Assert.Equal(-5, movement.QuantityDelta);
        Assert.Single(Added<PaymentEntity>(db));
        Assert.Single(Added<ReceiptEntity>(db));
        Assert.Single(Added<LedgerEntryEntity>(db));
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_StagesFinancialAndStockReversalOnce()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var service = CreateService(db);

        var result = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        Assert.Equal(PosSaleStateNames.Refunded, result.Sale!.State);
        Assert.Equal("player changed mind", result.Sale.RefundReason);
        Assert.Equal(Now, result.Sale.RefundedAtUtc);

        var reversal = AssertAddedSingle<LedgerEntryEntity>(db);
        var refundPayment = AssertAddedSingle<PaymentEntity>(db);
        var refundReceipt = AssertAddedSingle<ReceiptEntity>(db);
        var returnMovement = AssertAddedSingle<StockMovementEntity>(db);
        Assert.Equal(scenario.Debit.LedgerEntryId, reversal.ReversesLedgerEntryId);
        Assert.Equal(2_400, reversal.AmountMinorUnits);
        Assert.Equal(LedgerEntryTypeNames.Reversal, reversal.EntryType);
        Assert.Equal("refund", refundPayment.PaymentKind);
        Assert.Equal("wallet", refundPayment.Provider);
        Assert.Equal(PaymentMethodNames.Wallet, refundPayment.PaymentMethod);
        Assert.Equal(scenario.Sale.PosSaleId, refundPayment.PosSaleId);
        Assert.Equal(scenario.Sale.ShiftId, refundPayment.ShiftId);
        Assert.Equal(-2_400, refundPayment.AmountMinorUnits);
        Assert.Equal(reversal.LedgerEntryId, refundPayment.LedgerEntryId);
        Assert.Equal("refund", refundReceipt.ReceiptType);
        Assert.Equal(scenario.Sale.PosSaleId, refundReceipt.PosSaleId);
        Assert.Equal(2_400, refundReceipt.TotalMinorUnits);
        Assert.Equal(StockMovementTypeNames.Refund, returnMovement.MovementType);
        Assert.Equal(2, returnMovement.QuantityDelta);
        Assert.Equal(scenario.Line.UnitCostMinorUnits, returnMovement.UnitCostMinorUnits);
        Assert.Equal(scenario.Line.ProductId, returnMovement.ProductId);
        Assert.Same(reversal, result.WalletEntry);
        Assert.Same(refundReceipt, result.Receipt);
        Assert.Single(result.Lines);

        var productEntry = Assert.Single(
            db.ChangeTracker.Entries<PosProductEntity>(),
            entry => entry.Entity.ProductId == scenario.Line.ProductId);
        Assert.Equal(EntityState.Modified, productEntry.State);
        Assert.Equal(455, productEntry.Entity.AvgCostMinorUnits);
        Assert.Equal(1, await db.Payments.AsNoTracking().CountAsync());
        Assert.Equal(0, await db.Receipts.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.StockMovements.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.LedgerEntries.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_TrackedAtSale_RemainsRefundableAfterCurrentTrackingDisabled()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var product = await db.PosProducts.SingleAsync(candidate => candidate.ProductId == scenario.Line.ProductId);
        product.TrackStock = false;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var movement = AssertAddedSingle<StockMovementEntity>(db);
        Assert.Equal(StockMovementTypeNames.Refund, movement.MovementType);
        Assert.Equal(scenario.Line.UnitCostMinorUnits, movement.UnitCostMinorUnits);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_ProductCurrencyChangedFromSnapshot_FailsClosedWithoutStagedFinance()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var product = await db.PosProducts.SingleAsync(candidate => candidate.ProductId == scenario.Line.ProductId);
        product.CurrencyCode = "USD";
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("inventory_currency_mismatch", result.ErrorCode);
        AssertNoStagedCommerce(db);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_AlreadyRefunded_ReturnsSuccessWithoutDuplicates()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var existingReversal = new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = scenario.Sale.ShiftId,
            PlayerAccountId = PlayerAccountId,
            SessionId = SessionId,
            EntryType = LedgerEntryTypeNames.Reversal,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = 2_400,
            CurrencyCode = "TJS",
            Description = "shop_order_refund",
            Reason = "player changed mind",
            ReversesLedgerEntryId = scenario.Debit.LedgerEntryId,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        };
        var existingReceipt = new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = scenario.Sale.PosSaleId,
            ReceiptNumber = "REF-20260713-0001",
            ReceiptType = "refund",
            CurrencyCode = "TJS",
            TotalMinorUnits = 2_400,
            Locale = "ru",
            CreatedAtUtc = Now
        };
        var refundedSale = await db.PosSales.SingleAsync(candidate => candidate.PosSaleId == scenario.Sale.PosSaleId);
        refundedSale.State = PosSaleStateNames.Refunded;
        refundedSale.RefundReason = "player changed mind";
        refundedSale.RefundedAtUtc = Now;
        db.LedgerEntries.Add(existingReversal);
        db.Receipts.Add(existingReceipt);
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = scenario.Sale.PosSaleId,
            ShiftId = scenario.Sale.ShiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            PaymentKind = "refund",
            Provider = "wallet",
            PaymentMethod = PaymentMethodNames.Wallet,
            CurrencyCode = "TJS",
            AmountMinorUnits = -2_400,
            Note = "player changed mind",
            CreatedAtUtc = Now
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ProductId = scenario.Line.ProductId,
            MovementType = StockMovementTypeNames.Refund,
            QuantityDelta = 2,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = scenario.Line.UnitCostMinorUnits,
            Reason = "player changed mind",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(PosSaleStateNames.Refunded, result.Sale!.State);
        Assert.Equal(existingReversal.LedgerEntryId, result.WalletEntry!.LedgerEntryId);
        Assert.Equal(existingReceipt.ReceiptId, result.Receipt!.ReceiptId);
        Assert.Empty(Added<LedgerEntryEntity>(db));
        Assert.Empty(Added<PaymentEntity>(db));
        Assert.Empty(Added<ReceiptEntity>(db));
        Assert.Empty(Added<StockMovementEntity>(db));
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
        Assert.Equal(3, await db.LedgerEntries.AsNoTracking().CountAsync());
        Assert.Equal(2, await db.Payments.AsNoTracking().CountAsync());
        Assert.Equal(1, await db.Receipts.AsNoTracking().CountAsync());
        Assert.Equal(3, await db.StockMovements.AsNoTracking().CountAsync());
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_ImmediateReplay_ReturnsSameStagedArtifactsWithoutDuplicates()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var service = CreateService(db);
        var request = RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId);

        var first = await service.RefundPaidWalletSaleAsync(request, CancellationToken.None);
        var replay = await service.RefundPaidWalletSaleAsync(request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equal(first.WalletEntry!.LedgerEntryId, replay.WalletEntry!.LedgerEntryId);
        Assert.Equal(first.Receipt!.ReceiptId, replay.Receipt!.ReceiptId);
        Assert.Single(Added<LedgerEntryEntity>(db));
        Assert.Single(Added<PaymentEntity>(db));
        Assert.Single(Added<ReceiptEntity>(db));
        Assert.Single(Added<StockMovementEntity>(db));
        Assert.Single(
            db.ChangeTracker.Entries<PosProductEntity>(),
            entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_AlreadyRefundedWithUnrelatedDebit_ReturnsWalletDebitMismatch()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var service = CreateService(db);
        var settled = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);
        Assert.True(settled.Succeeded);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var unrelatedDebit = await db.LedgerEntries
            .AsNoTracking()
            .SingleAsync(entry => entry.EntryType == LedgerEntryTypeNames.TopUp);
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = unrelatedDebit.OrganizationId,
            BranchId = unrelatedDebit.BranchId,
            PlayerAccountId = unrelatedDebit.PlayerAccountId,
            EntryType = LedgerEntryTypeNames.Reversal,
            AccountType = unrelatedDebit.AccountType,
            AmountMinorUnits = -unrelatedDebit.AmountMinorUnits,
            CurrencyCode = unrelatedDebit.CurrencyCode,
            Description = "unrelated",
            Reason = "unrelated",
            ReversesLedgerEntryId = unrelatedDebit.LedgerEntryId,
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var replay = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, unrelatedDebit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(replay.Succeeded);
        Assert.Equal("wallet_debit_mismatch", replay.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_AlreadyRefundedWithInvalidRefundPayment_ReturnsRefundIncomplete()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var service = CreateService(db);
        var settled = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);
        Assert.True(settled.Succeeded);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var refundPayment = await db.Payments.SingleAsync(payment => payment.PaymentKind == "refund");
        refundPayment.AmountMinorUnits = -1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var replay = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(replay.Succeeded);
        Assert.Equal("refund_incomplete", replay.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_AlreadyRefundedWithInvalidReversal_ReturnsRefundIncomplete()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var service = CreateService(db);
        var settled = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);
        Assert.True(settled.Succeeded);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var reversal = await db.LedgerEntries.SingleAsync(
            entry => entry.ReversesLedgerEntryId == scenario.Debit.LedgerEntryId);
        reversal.AmountMinorUnits = 1;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var replay = await service.RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(replay.Succeeded);
        Assert.Equal("refund_incomplete", replay.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_NonPaidSale_ReturnsSaleNotRefundable()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var draftSale = await db.PosSales.SingleAsync(candidate => candidate.PosSaleId == scenario.Sale.PosSaleId);
        draftSale.State = PosSaleStateNames.Draft;
        draftSale.PaidAtUtc = null;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, scenario.Debit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("sale_not_refundable", result.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_LaterInvalidCostGroup_StagesNothing()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);
        var secondProduct = await SeedProductAsync(db, "Snack", 1_200, trackStock: true, avgCostMinorUnits: 300);
        await SeedStockAsync(db, secondProduct.ProductId, quantity: 10, unitCostMinorUnits: 300);
        var sale = await db.PosSales.SingleAsync(candidate => candidate.PosSaleId == scenario.Sale.PosSaleId);
        var debit = await db.LedgerEntries.SingleAsync(candidate => candidate.LedgerEntryId == scenario.Debit.LedgerEntryId);
        var payment = await db.Payments.SingleAsync(candidate => candidate.PosSaleId == scenario.Sale.PosSaleId);
        sale.TotalMinorUnits = 4_800;
        debit.AmountMinorUnits = -4_800;
        payment.AmountMinorUnits = 4_800;
        db.PosSaleLines.AddRange(
            new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(), PosSaleId = sale.PosSaleId, ProductId = secondProduct.ProductId,
                ProductName = secondProduct.Name, Quantity = 1, CurrencyCode = "TJS", UnitPriceMinorUnits = 1_200,
                UnitCostMinorUnits = 300, LineTotalMinorUnits = 1_200, TracksStock = true
            },
            new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(), PosSaleId = sale.PosSaleId, ProductId = secondProduct.ProductId,
                ProductName = secondProduct.Name, Quantity = 1, CurrencyCode = "TJS", UnitPriceMinorUnits = 1_200,
                UnitCostMinorUnits = 400, LineTotalMinorUnits = 1_200, TracksStock = true
            });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(sale.PosSaleId, debit.LedgerEntryId),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("cost_snapshot_conflict", result.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task RefundPaidWalletSaleAsync_MissingCanonicalDebit_StagesNothing()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedPaidSaleAsync(db);

        var result = await CreateService(db).RefundPaidWalletSaleAsync(
            RefundRequest(scenario.Sale.PosSaleId, Guid.NewGuid()),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("original_debit_not_found", result.ErrorCode);
        AssertNoStagedCommerce(db);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    private static ShopPosSaleRequest SaleRequest(params ShopOrderLineInput[] lines) =>
        new(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            SessionId,
            ActorStaffUserId,
            lines,
            "shop-order-123",
            Now);

    private static ShopPosRefundRequest RefundRequest(Guid posSaleId, Guid walletLedgerEntryId) =>
        new(
            posSaleId,
            Guid.Parse("99999999-9999-4999-8999-999999999999"),
            walletLedgerEntryId,
            ActorStaffUserId,
            "player changed mind",
            Now);

    private static EfShopPosSettlementService CreateService(PlatformDbContext db) =>
        new(
            db,
            new EfWalletSettlementService(db),
            new EfInventoryCostService(db),
            new ReceiptNumberGenerator(db));

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static async Task SeedBranchAndPlayerAsync(PlatformDbContext db, long balanceMinorUnits)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Name = "Central",
            PreferredLocale = "ru",
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = OrganizationId,
            HomeBranchId = BranchId,
            DisplayName = "Player",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.LedgerEntries.Add(new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PlayerAccountId = PlayerAccountId,
            EntryType = LedgerEntryTypeNames.TopUp,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = balanceMinorUnits,
            CurrencyCode = "TJS",
            Description = "seed",
            Reason = "test",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddMinutes(-10)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<ShiftEntity> SeedOpenShiftAsync(PlatformDbContext db)
    {
        var shift = new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            OpeningNote = "front register",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now.AddHours(-1)
        };
        db.Shifts.Add(shift);
        await db.SaveChangesAsync();
        return shift;
    }

    private static async Task<PosProductEntity> SeedProductAsync(
        PlatformDbContext db,
        string name,
        long priceMinorUnits,
        bool trackStock,
        long avgCostMinorUnits,
        bool isActive = true,
        bool availableInShell = true,
        bool allowNegativeStock = false,
        string currencyCode = "TJS")
    {
        var product = new PosProductEntity
        {
            ProductId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            CategoryId = Guid.NewGuid(),
            Name = name,
            Sku = $"SKU-{Guid.NewGuid():N}",
            CurrencyCode = currencyCode,
            PriceMinorUnits = priceMinorUnits,
            AvgCostMinorUnits = avgCostMinorUnits,
            TrackStock = trackStock,
            AllowNegativeStock = allowNegativeStock,
            IsActive = isActive,
            AvailableInShell = availableInShell,
            CreatedAtUtc = Now
        };
        db.PosProducts.Add(product);
        await db.SaveChangesAsync();
        return product;
    }

    private static async Task SeedStockAsync(
        PlatformDbContext db,
        Guid productId,
        int quantity,
        long unitCostMinorUnits)
    {
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ProductId = productId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = quantity,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = unitCostMinorUnits,
            Reason = "test stock",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddMinutes(-5)
        });
        await db.SaveChangesAsync();
    }

    private static async Task<PaidSaleScenario> SeedPaidSaleAsync(PlatformDbContext db)
    {
        await SeedBranchAndPlayerAsync(db, balanceMinorUnits: 10_000);
        var shift = await SeedOpenShiftAsync(db);
        var product = await SeedProductAsync(db, "Cola", 1_200, trackStock: true, avgCostMinorUnits: 500);
        await SeedStockAsync(db, product.ProductId, quantity: 10, unitCostMinorUnits: 275);
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ProductId = product.ProductId,
            MovementType = StockMovementTypeNames.Sale,
            QuantityDelta = -2,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 275,
            Reason = "original sale",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddMinutes(-2)
        });
        var sale = new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = shift.ShiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerAccountId = PlayerAccountId,
            SessionId = SessionId,
            State = PosSaleStateNames.Paid,
            CurrencyCode = "TJS",
            TotalMinorUnits = 2_400,
            CreatedAtUtc = Now.AddMinutes(-2),
            PaidAtUtc = Now.AddMinutes(-2)
        };
        var line = new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = sale.PosSaleId,
            ProductId = product.ProductId,
            ProductName = product.Name,
            Quantity = 2,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = 1_200,
            UnitCostMinorUnits = 275,
            LineTotalMinorUnits = 2_400,
            TracksStock = true
        };
        var debit = new LedgerEntryEntity
        {
            LedgerEntryId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = shift.ShiftId,
            PlayerAccountId = PlayerAccountId,
            SessionId = SessionId,
            EntryType = LedgerEntryTypeNames.WalletPayment,
            AccountType = LedgerAccountTypeNames.Wallet,
            AmountMinorUnits = -2_400,
            CurrencyCode = "TJS",
            Description = "shop_order",
            Reason = "shop-order-123",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddMinutes(-2)
        };
        db.PosSales.Add(sale);
        db.PosSaleLines.Add(line);
        db.LedgerEntries.Add(debit);
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            PosSaleId = sale.PosSaleId,
            ShiftId = shift.ShiftId,
            CreatedByStaffUserId = ActorStaffUserId,
            PaymentKind = "payment",
            Provider = "wallet",
            PaymentMethod = PaymentMethodNames.Wallet,
            CurrencyCode = "TJS",
            AmountMinorUnits = 2_400,
            Note = "shop-order-123",
            CreatedAtUtc = Now.AddMinutes(-2)
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return new PaidSaleScenario(sale, line, debit);
    }

    private static List<T> Added<T>(PlatformDbContext db) where T : class =>
        db.ChangeTracker.Entries<T>()
            .Where(entry => entry.State == EntityState.Added)
            .Select(entry => entry.Entity)
            .ToList();

    private static T AssertAddedSingle<T>(PlatformDbContext db) where T : class =>
        Assert.Single(Added<T>(db));

    private static void AssertNoStagedCommerce(PlatformDbContext db)
    {
        Assert.Empty(Added<PosSaleEntity>(db));
        Assert.Empty(Added<PosSaleLineEntity>(db));
        Assert.Empty(Added<PaymentEntity>(db));
        Assert.Empty(Added<ReceiptEntity>(db));
        Assert.Empty(Added<StockMovementEntity>(db));
        Assert.Empty(Added<LedgerEntryEntity>(db));
        Assert.DoesNotContain(
            db.ChangeTracker.Entries<PosProductEntity>(),
            entry => entry.State == EntityState.Modified);
    }

    private sealed record PaidSaleScenario(
        PosSaleEntity Sale,
        PosSaleLineEntity Line,
        LedgerEntryEntity Debit);
}
