using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Tests.Billing;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfPosSettlementServiceTests
{
    private static readonly Guid OrganizationId = TestIds.OrganizationId;
    private static readonly Guid BranchId = TestIds.BranchId;
    private static readonly Guid ActorId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    private static readonly DateTimeOffset Now = new(2026, 7, 14, 8, 0, 0, TimeSpan.Zero);

    public static TheoryData<IReadOnlyList<PaymentPartDto>> InvalidSplits => new()
    {
        Array.Empty<PaymentPartDto>(),
        new PaymentPartDto[] { Part("cash", 4_000), Part("cash", 6_000) },
        new PaymentPartDto[] { Part("crypto", 10_000) },
        new PaymentPartDto[] { Part("cash", 0) },
        new PaymentPartDto[] { Part("cash", -1) },
        new PaymentPartDto[] { Part("cash", 9_999) }
    };

    [Theory]
    [MemberData(nameof(InvalidSplits))]
    public async Task SettleAsync_InvalidSplit_RejectsWithoutSideEffects(IReadOnlyList<PaymentPartDto> payments)
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request(payments, "invalid-split"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_payment_split", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_MixedCurrency_ReturnsStableErrorWithoutSideEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 4_000), Part("card_manual", 6_000, "USD")], "mixed-currency"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("mixed_currency", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_WalletWithoutPlayer_RejectsWithoutSideEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 10_000)], "wallet-no-player"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("wallet_player_required", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_InsufficientWallet_RejectsWithoutSideEffects()
    {
        await using var db = CreateDbContext();
        var playerId = Guid.NewGuid();
        var scenario = await SeedSaleAsync(db, playerId: playerId, walletBalanceMinorUnits: 3_999);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 4_000), Part("cash", 6_000)], "wallet-short"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("insufficient_funds", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
        Assert.Single(await db.LedgerEntries.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task SettleAsync_ClosedOrMismatchedShift_RejectsWithoutSideEffects(
        bool closeShift,
        bool mismatchCurrency)
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        scenario.Shift.State = closeShift ? ShiftStateNames.Closed : ShiftStateNames.Open;
        scenario.Shift.CurrencyCode = mismatchCurrency ? "USD" : "TJS";
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], $"shift-{closeShift}-{mismatchCurrency}"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("open_shift_required", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_WalletAndCash_CommitsLinkedPartsReceiptTrackedStockAndPaidSale()
    {
        await using var db = CreateDbContext();
        var playerId = Guid.NewGuid();
        var scenario = await SeedSaleAsync(db, playerId: playerId, walletBalanceMinorUnits: 15_000);
        var notifier = new RecordingLowStockNotifier();
        var service = CreateService(db, lowStockNotifier: notifier);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("WALLET", 4_000, "tjs"), Part(" cash ", 6_000)], "split-success"),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.NotNull(result.Response);
        Assert.Equal(PosSaleStateNames.Paid, result.Response.State);
        Assert.Equal(Now, result.Response.PaidAtUtc);

        var payments = await db.Payments.AsNoTracking()
            .Where(payment => payment.PosSaleId == scenario.Sale.PosSaleId)
            .OrderBy(payment => payment.PaymentMethod)
            .ToListAsync();
        Assert.Equal(2, payments.Count);
        var cash = Assert.Single(payments, payment => payment.PaymentMethod == PaymentMethodNames.Cash);
        var wallet = Assert.Single(payments, payment => payment.PaymentMethod == PaymentMethodNames.Wallet);
        Assert.Equal(6_000, cash.AmountMinorUnits);
        Assert.Null(cash.LedgerEntryId);
        Assert.Equal(4_000, wallet.AmountMinorUnits);
        Assert.NotNull(wallet.LedgerEntryId);
        var debit = await db.LedgerEntries.AsNoTracking()
            .SingleAsync(entry => entry.LedgerEntryId == wallet.LedgerEntryId);
        Assert.Equal(-4_000, debit.AmountMinorUnits);
        Assert.Equal(playerId, debit.PlayerAccountId);

        var receipt = await db.Receipts.AsNoTracking().SingleAsync();
        Assert.Equal("sale", receipt.ReceiptType);
        Assert.Equal(10_000, receipt.TotalMinorUnits);
        var movement = await db.StockMovements.AsNoTracking()
            .SingleAsync(candidate => candidate.MovementType == StockMovementTypeNames.Sale);
        Assert.Equal(scenario.TrackedProductId, movement.ProductId);
        Assert.Equal(-2, movement.QuantityDelta);
        Assert.Equal(450, movement.UnitCostMinorUnits);
        Assert.DoesNotContain(
            await db.StockMovements.AsNoTracking().ToListAsync(),
            candidate => candidate.ProductId == scenario.ServiceProductId);
        Assert.Equal([scenario.TrackedProductId], Assert.Single(notifier.Calls).ProductIds);
    }

    [Fact]
    public async Task SettleAsync_ReceiptFailure_RollsBackEveryStagedMutation()
    {
        await using var db = CreateDbContext();
        var playerId = Guid.NewGuid();
        var scenario = await SeedSaleAsync(db, playerId: playerId, walletBalanceMinorUnits: 15_000);
        var service = CreateService(db, receiptNumberGenerator: new ThrowingReceiptNumberGenerator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 4_000), Part("cash", 6_000)], "receipt-failure"),
            CancellationToken.None));

        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
        var ledger = await db.LedgerEntries.AsNoTracking().ToListAsync();
        Assert.Single(ledger);
        Assert.Equal(15_000, ledger[0].AmountMinorUnits);
    }

    [Fact]
    public async Task SettleAsync_NullPaymentElement_RejectsWithoutSideEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([null!], "null-payment"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_payment_split", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_NullPaymentAmount_RejectsWithoutSideEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([new PaymentPartDto("cash", null!)], "null-amount"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_payment_split", result.Error);
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    [Fact]
    public async Task SettleAsync_ThrowingPostCommitNotifier_ReturnsDurableSuccess()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db, lowStockNotifier: new ThrowingLowStockNotifier());

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], "notify-throws"),
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(PosSaleStateNames.Paid, (await db.PosSales.AsNoTracking().SingleAsync()).State);
        Assert.Single(await db.Payments.AsNoTracking().ToListAsync());
        Assert.Single(await db.Receipts.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SettleAsync_CancelledRequestAfterCommit_ReturnsDurableSuccess()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        using var requestCancellation = new CancellationTokenSource();
        var notifier = new CancellingLowStockNotifier(requestCancellation);
        var service = CreateService(db, lowStockNotifier: notifier);

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], "notify-cancelled"),
            requestCancellation.Token);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(CancellationToken.None, notifier.ReceivedToken);
        Assert.Equal(PosSaleStateNames.Paid, (await db.PosSales.AsNoTracking().SingleAsync()).State);
        Assert.Single(await db.Payments.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task SettleAsync_SameNormalizedRequest_ReplaysWithoutDuplicateSideEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var first = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 4_000), Part("card_manual", 6_000)], "replay"),
            CancellationToken.None);
        var replay = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part(" CARD_MANUAL ", 6_000, "tjs"), Part("CASH", 4_000)], "replay"),
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);
        Assert.True(replay.Succeeded, replay.Error);
        Assert.Equal(first.Response!.PosSaleId, replay.Response!.PosSaleId);
        Assert.Equal(first.Response.State, replay.Response.State);
        Assert.Equal(first.Response.LatestReceipt!.ReceiptId, replay.Response.LatestReceipt!.ReceiptId);
        Assert.Equal(2, await db.Payments.CountAsync());
        Assert.Single(await db.Receipts.ToListAsync());
        Assert.Single(await db.BillingCommandIdempotency.ToListAsync());
    }

    [Fact]
    public async Task SettleAsync_ReusedKeyForDifferentNormalizedRequest_ReturnsIdempotencyConflict()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);

        var first = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], "conflict"),
            CancellationToken.None);
        var conflict = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("card_manual", 10_000)], "conflict"),
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("idempotency_conflict", conflict.Error);
        Assert.Single(await db.Payments.ToListAsync());
    }

    [Fact]
    public async Task RefundAsync_WalletAndCash_ReversesOriginalMixAndReplaysWithoutDuplicateEffects()
    {
        await using var db = CreateDbContext();
        var playerId = Guid.NewGuid();
        var scenario = await SeedSaleAsync(db, playerId: playerId, walletBalanceMinorUnits: 15_000);
        var service = CreateService(db);

        var settled = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 4_000), Part("cash", 6_000)], "mixed-refund-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);

        var walletDebitId = await db.Payments
            .Where(payment =>
                payment.PosSaleId == scenario.Sale.PosSaleId &&
                payment.PaymentKind == "payment" &&
                payment.PaymentMethod == PaymentMethodNames.Wallet)
            .Select(payment => payment.LedgerEntryId!.Value)
            .SingleAsync();
        var request = new RefundPosSaleRequest(
            OrganizationId,
            "customer returned items",
            "mixed-refund");

        var result = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            request,
            CancellationToken.None);
        var replay = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            request,
            CancellationToken.None);

        Assert.True(result.Succeeded, result.Error);
        Assert.True(replay.Succeeded, replay.Error);
        Assert.Equal(result.Response!.LatestReceipt!.ReceiptId, replay.Response!.LatestReceipt!.ReceiptId);
        var refundPayments = await db.Payments
            .Where(payment =>
                payment.PosSaleId == scenario.Sale.PosSaleId &&
                payment.PaymentKind == "refund")
            .ToListAsync();
        Assert.Equal(
            [-6_000L, -4_000L],
            refundPayments.OrderBy(payment => payment.AmountMinorUnits).Select(payment => payment.AmountMinorUnits));
        Assert.Single(await db.LedgerEntries
            .Where(entry => entry.ReversesLedgerEntryId == walletDebitId)
            .ToListAsync());
        var reversal = await db.LedgerEntries
            .SingleAsync(entry => entry.ReversesLedgerEntryId == walletDebitId);
        var originalPayments = await db.Payments
            .Where(payment =>
                payment.PosSaleId == scenario.Sale.PosSaleId &&
                payment.PaymentKind == "payment")
            .ToListAsync();
        Assert.All(originalPayments, original =>
        {
            var refund = Assert.Single(refundPayments, candidate =>
                candidate.PaymentMethod == original.PaymentMethod);
            Assert.Equal(original.Provider, refund.Provider);
            Assert.Equal(original.PaymentMethod, refund.PaymentMethod);
            Assert.Equal(-original.AmountMinorUnits, refund.AmountMinorUnits);
        });
        Assert.Equal(
            reversal.LedgerEntryId,
            Assert.Single(refundPayments, payment =>
                payment.PaymentMethod == PaymentMethodNames.Wallet).LedgerEntryId);
        Assert.Single(await db.Receipts
            .Where(receipt =>
                receipt.PosSaleId == scenario.Sale.PosSaleId &&
                receipt.ReceiptType == "refund")
            .ToListAsync());
        Assert.Single(await db.StockMovements
            .Where(movement => movement.MovementType == StockMovementTypeNames.Refund)
            .ToListAsync());
        Assert.Equal(2, await db.BillingCommandIdempotency.CountAsync());
    }

    [Fact]
    public async Task RefundAsync_PaidSaleWithPriorRefundEffect_FailsClosedWithoutDuplicatingIt()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);
        var settled = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], "prior-refund-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);
        db.Payments.Add(new PaymentEntity
        {
            PaymentId = Guid.NewGuid(), OrganizationId = OrganizationId, BranchId = BranchId,
            PosSaleId = scenario.Sale.PosSaleId, ShiftId = scenario.Shift.ShiftId,
            CreatedByStaffUserId = ActorId, PaymentKind = "refund", Provider = "manual",
            PaymentMethod = PaymentMethodNames.Cash, CurrencyCode = "TJS", AmountMinorUnits = -1_000,
            Note = "legacy partial effect", CreatedAtUtc = Now
        });
        await db.SaveChangesAsync();

        var result = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "return", "prior-refund"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("refund_incomplete", result.Error);
        Assert.Single(await db.Payments.Where(payment => payment.PaymentKind == "refund").ToListAsync());
        Assert.Empty(await db.Receipts.Where(receipt => receipt.ReceiptType == "refund").ToListAsync());
        Assert.Empty(await db.StockMovements
            .Where(movement => movement.MovementType == StockMovementTypeNames.Refund)
            .ToListAsync());
    }

    [Fact]
    public async Task RefundAsync_WalletPaymentWithoutLedgerLink_FailsClosed()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db, Guid.NewGuid(), 15_000);
        var service = CreateService(db);
        var settled = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 4_000), Part("cash", 6_000)], "missing-link-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);
        var walletPayment = await db.Payments.SingleAsync(payment =>
            payment.PosSaleId == scenario.Sale.PosSaleId &&
            payment.PaymentMethod == PaymentMethodNames.Wallet);
        walletPayment.LedgerEntryId = null;
        await db.SaveChangesAsync();

        var result = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "return", "missing-link-refund"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("payment_snapshot_invalid", result.Error);
        Assert.Empty(await db.Payments.Where(payment => payment.PaymentKind == "refund").ToListAsync());
        Assert.Empty(await db.LedgerEntries.Where(entry => entry.ReversesLedgerEntryId != null).ToListAsync());
        Assert.Equal(PosSaleStateNames.Paid, (await db.PosSales.SingleAsync()).State);
    }

    [Fact]
    public async Task RefundAsync_ReusedKeyForDifferentReason_ReturnsVersionConflictWithoutNewEffects()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db);
        var service = CreateService(db);
        var settled = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("cash", 10_000)], "refund-conflict-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);

        var first = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "first reason", "refund-conflict"),
            CancellationToken.None);
        var conflict = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "different reason", "refund-conflict"),
            CancellationToken.None);

        Assert.True(first.Succeeded, first.Error);
        Assert.False(conflict.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("version_conflict", conflict.Error);
        Assert.Single(await db.Payments.Where(payment => payment.PaymentKind == "refund").ToListAsync());
        Assert.Single(await db.Receipts.Where(receipt => receipt.ReceiptType == "refund").ToListAsync());
    }

    [Fact]
    public async Task RefundAsync_ReceiptFailure_RollsBackReversalPaymentsStockAndSaleState()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db, Guid.NewGuid(), 15_000);
        var settlementService = CreateService(db);
        var settled = await settlementService.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 4_000), Part("cash", 6_000)], "refund-rollback-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);
        var refundService = CreateService(db, new ThrowingReceiptNumberGenerator());

        await Assert.ThrowsAsync<InvalidOperationException>(() => refundService.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "return", "refund-rollback"),
            CancellationToken.None));

        Assert.Empty(await db.Payments.AsNoTracking().Where(payment => payment.PaymentKind == "refund").ToListAsync());
        Assert.Empty(await db.LedgerEntries.AsNoTracking().Where(entry => entry.ReversesLedgerEntryId != null).ToListAsync());
        Assert.Empty(await db.Receipts.AsNoTracking().Where(receipt => receipt.ReceiptType == "refund").ToListAsync());
        Assert.Empty(await db.StockMovements.AsNoTracking()
            .Where(movement => movement.MovementType == StockMovementTypeNames.Refund)
            .ToListAsync());
        Assert.Equal(PosSaleStateNames.Paid, (await db.PosSales.AsNoTracking().SingleAsync()).State);
    }

    [Fact]
    public async Task RefundAsync_InventoryQuantityOverflow_DiscardsStagedWalletEffectsBeforeCallerSave()
    {
        await using var db = CreateDbContext();
        var playerId = Guid.NewGuid();
        var scenario = await SeedSaleAsync(db, playerId, 15_000);
        var trackedLine = await db.PosSaleLines.SingleAsync(line => line.ProductId == scenario.TrackedProductId);
        trackedLine.Quantity = int.MaxValue;
        trackedLine.UnitPriceMinorUnits = 0;
        trackedLine.LineTotalMinorUnits = 0;
        trackedLine.AllowNegativeStock = true;
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(), PosSaleId = scenario.Sale.PosSaleId,
            ProductId = scenario.TrackedProductId, ProductName = trackedLine.ProductName,
            Quantity = 1, CurrencyCode = "TJS", UnitPriceMinorUnits = 0,
            UnitCostMinorUnits = trackedLine.UnitCostMinorUnits, LineTotalMinorUnits = 0,
            TracksStock = true, AllowNegativeStock = true
        });
        scenario.Sale.TotalMinorUnits = 4_000;
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var settled = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 1_000), Part("cash", 3_000)], "overflow-payment"),
            CancellationToken.None);
        Assert.True(settled.Succeeded, settled.Error);
        var effectsBeforeRefund = await db.BillingCommandIdempotency.CountAsync();

        var result = await service.RefundAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            new RefundPosSaleRequest(OrganizationId, "return", "overflow-refund"),
            CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("sale_snapshot_invalid", result.Error);

        await db.SaveChangesAsync();
        Assert.Empty(await db.LedgerEntries.AsNoTracking()
            .Where(entry => entry.ReversesLedgerEntryId != null)
            .ToListAsync());
        Assert.Empty(await db.Payments.AsNoTracking()
            .Where(payment => payment.PaymentKind == "refund")
            .ToListAsync());
        Assert.Empty(await db.Receipts.AsNoTracking()
            .Where(receipt => receipt.ReceiptType == "refund")
            .ToListAsync());
        Assert.Empty(await db.StockMovements.AsNoTracking()
            .Where(movement => movement.MovementType == StockMovementTypeNames.Refund)
            .ToListAsync());
        Assert.Equal(effectsBeforeRefund, await db.BillingCommandIdempotency.CountAsync());
        Assert.Equal(PosSaleStateNames.Paid, (await db.PosSales.AsNoTracking().SingleAsync()).State);
    }

    [Fact]
    public async Task SettleAsync_FailedWalletService_DiscardsItsStagedEffectBeforeCallerSave()
    {
        await using var db = CreateDbContext();
        var scenario = await SeedSaleAsync(db, Guid.NewGuid(), 15_000);
        var service = CreateService(
            db,
            walletSettlementService: new StagingRejectedWalletSettlementService(db));
        var ledgerCountBefore = await db.LedgerEntries.CountAsync();

        var result = await service.SettleAsync(
            scenario.Sale.PosSaleId,
            ActorId,
            Request([Part("wallet", 10_000)], "staged-wallet-failure"),
            CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal("forced_wallet_failure", result.Error);

        await db.SaveChangesAsync();
        Assert.Equal(ledgerCountBefore, await db.LedgerEntries.AsNoTracking().CountAsync());
        await AssertNoSettlementSideEffectsAsync(db, scenario.Sale.PosSaleId);
    }

    private static EfPosSettlementService CreateService(
        PlatformDbContext db,
        IReceiptNumberGenerator? receiptNumberGenerator = null,
        ILowStockNotifier? lowStockNotifier = null,
        IWalletSettlementService? walletSettlementService = null) =>
        new(
            db,
            walletSettlementService ?? new EfWalletSettlementService(db),
            new EfInventoryCostService(db),
            receiptNumberGenerator ?? new ReceiptNumberGenerator(db),
            new FixedTimeProvider(Now),
            lowStockNotifier);

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static async Task<Scenario> SeedSaleAsync(
        PlatformDbContext db,
        Guid? playerId = null,
        long walletBalanceMinorUnits = 0)
    {
        var shift = new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            OpenedByStaffUserId = ActorId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            OpenedAtUtc = Now
        };
        var sale = new PosSaleEntity
        {
            PosSaleId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            ShiftId = shift.ShiftId,
            CreatedByStaffUserId = ActorId,
            PlayerAccountId = playerId,
            State = PosSaleStateNames.Draft,
            CurrencyCode = "TJS",
            TotalMinorUnits = 10_000,
            CreatedAtUtc = Now
        };
        var trackedProductId = Guid.NewGuid();
        var serviceProductId = Guid.NewGuid();

        db.Branches.Add(new BranchEntity
        {
            BranchId = BranchId,
            OrganizationId = OrganizationId,
            Name = "Central",
            PreferredLocale = "ru",
            CreatedAtUtc = Now
        });
        db.Shifts.Add(shift);
        db.PosSales.Add(sale);
        db.PosSaleLines.AddRange(
            new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(), PosSaleId = sale.PosSaleId, ProductId = trackedProductId,
                ProductName = "Tracked", Quantity = 2, CurrencyCode = "TJS", UnitPriceMinorUnits = 3_000,
                UnitCostMinorUnits = 450, LineTotalMinorUnits = 6_000, TracksStock = true
            },
            new PosSaleLineEntity
            {
                PosSaleLineId = Guid.NewGuid(), PosSaleId = sale.PosSaleId, ProductId = serviceProductId,
                ProductName = "Service", Quantity = 1, CurrencyCode = "TJS", UnitPriceMinorUnits = 4_000,
                UnitCostMinorUnits = 0, LineTotalMinorUnits = 4_000, TracksStock = false
            });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(), OrganizationId = OrganizationId, BranchId = BranchId,
            ProductId = trackedProductId, MovementType = StockMovementTypeNames.Purchase, QuantityDelta = 5,
            CurrencyCode = "TJS", UnitCostMinorUnits = 450, Reason = "seed", CreatedByStaffUserId = ActorId,
            CreatedAtUtc = Now
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = trackedProductId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            Name = "Tracked",
            CurrencyCode = "TJS",
            PriceMinorUnits = 3_000,
            AvgCostMinorUnits = 450,
            TrackStock = true,
            IsActive = true,
            CreatedAtUtc = Now
        });

        if (playerId is Guid accountId)
        {
            db.PlayerAccounts.Add(new PlayerAccountEntity
            {
                PlayerAccountId = accountId, OrganizationId = OrganizationId, HomeBranchId = BranchId,
                DisplayName = "Player", IsActive = true, CreatedAtUtc = Now
            });
            if (walletBalanceMinorUnits > 0)
            {
                db.LedgerEntries.Add(new LedgerEntryEntity
                {
                    LedgerEntryId = Guid.NewGuid(), OrganizationId = OrganizationId, BranchId = BranchId,
                    PlayerAccountId = accountId, EntryType = LedgerEntryTypeNames.ManualCorrection,
                    AccountType = LedgerAccountTypeNames.Wallet, AmountMinorUnits = walletBalanceMinorUnits,
                    CurrencyCode = "TJS", Description = "seed", Reason = "seed", CreatedByStaffUserId = ActorId,
                    CreatedAtUtc = Now
                });
            }
        }

        await db.SaveChangesAsync();
        return new Scenario(sale, shift, trackedProductId, serviceProductId);
    }

    private static async Task AssertNoSettlementSideEffectsAsync(PlatformDbContext db, Guid saleId)
    {
        db.ChangeTracker.Clear();
        Assert.Empty(await db.Payments.AsNoTracking().Where(payment => payment.PosSaleId == saleId).ToListAsync());
        Assert.Empty(await db.Receipts.AsNoTracking().Where(receipt => receipt.PosSaleId == saleId).ToListAsync());
        Assert.Empty(await db.StockMovements.AsNoTracking()
            .Where(movement => movement.MovementType == StockMovementTypeNames.Sale).ToListAsync());
        Assert.Empty(await db.BillingCommandIdempotency.AsNoTracking().ToListAsync());
        Assert.Equal(PosSaleStateNames.Draft, (await db.PosSales.AsNoTracking().SingleAsync()).State);
    }

    private static SettlePosSaleRequest Request(IReadOnlyList<PaymentPartDto> payments, string key) =>
        new(OrganizationId, payments, "operator checkout", key);

    private static PaymentPartDto Part(string method, long amount, string currency = "TJS") =>
        new(method, new MoneyDto(currency, amount));

    private sealed record Scenario(
        PosSaleEntity Sale,
        ShiftEntity Shift,
        Guid TrackedProductId,
        Guid ServiceProductId);

    private sealed class ThrowingReceiptNumberGenerator : IReceiptNumberGenerator
    {
        public Task<string> GenerateAsync(
            Guid organizationId,
            Guid branchId,
            string receiptType,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("receipt failure");
    }

    private sealed class RecordingLowStockNotifier : ILowStockNotifier
    {
        public List<Call> Calls { get; } = [];

        public Task EvaluateProductsAsync(
            Guid organizationId,
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
        {
            Calls.Add(new Call(organizationId, branchId, productIds.ToArray()));
            return Task.CompletedTask;
        }

        public sealed record Call(Guid OrganizationId, Guid BranchId, IReadOnlyList<Guid> ProductIds);
    }

    private sealed class ThrowingLowStockNotifier : ILowStockNotifier
    {
        public Task EvaluateProductsAsync(
            Guid organizationId,
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("notification failure");
    }

    private sealed class CancellingLowStockNotifier(CancellationTokenSource requestCancellation) : ILowStockNotifier
    {
        public CancellationToken ReceivedToken { get; private set; }

        public Task EvaluateProductsAsync(
            Guid organizationId,
            Guid branchId,
            IReadOnlyCollection<Guid> productIds,
            CancellationToken cancellationToken)
        {
            ReceivedToken = cancellationToken;
            requestCancellation.Cancel();
            throw new OperationCanceledException(requestCancellation.Token);
        }
    }

    private sealed class StagingRejectedWalletSettlementService(PlatformDbContext db) : IWalletSettlementService
    {
        public Task<WalletSettlementResult> DebitAsync(
            Guid organizationId,
            Guid branchId,
            Guid playerAccountId,
            Guid? sessionId,
            Guid shiftId,
            long amountMinorUnits,
            string currencyCode,
            string description,
            string reason,
            Guid actorStaffUserId,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            db.LedgerEntries.Add(new LedgerEntryEntity
            {
                LedgerEntryId = Guid.NewGuid(), OrganizationId = organizationId, BranchId = branchId,
                PlayerAccountId = playerAccountId, ShiftId = shiftId,
                EntryType = LedgerEntryTypeNames.WalletPayment, AccountType = LedgerAccountTypeNames.Wallet,
                AmountMinorUnits = -amountMinorUnits, CurrencyCode = currencyCode,
                Description = description, Reason = reason, CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            });
            return Task.FromResult(WalletSettlementResult.Reject("forced_wallet_failure"));
        }

        public Task<WalletSettlementResult> ReverseAsync(
            LedgerEntryEntity originalDebit,
            Guid actorStaffUserId,
            string description,
            string reason,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}
