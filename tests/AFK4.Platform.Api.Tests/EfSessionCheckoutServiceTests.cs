using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Sessions;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests;

public sealed class EfSessionCheckoutServiceTests
{
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ZoneId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid ActorStaffUserId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid PlayerAccountId = Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb");
    private static readonly Guid TariffVersionId = Guid.Parse("cccccccc-cccc-4ccc-8ccc-cccccccccccc");
    private static readonly Guid SessionId = Guid.Parse("dddddddd-dddd-4ddd-8ddd-dddddddddddd");
    private static readonly Guid ProductId = Guid.Parse("eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-05-13T10:00:00Z");

    // 40 min elapsed at 50/min, min 30, increment 15 -> billable 45 -> 2250.
    private const long ExpectedTimeCharge = 2250;

    [Fact]
    public async Task CheckoutAsync_OpenTabPostpaid_TimeOnly_NetsDebtAndLocks()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedOpenPostpaidSessionAsync(db);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);

        var result = await service.CheckoutAsync(
            SessionId,
            ActorStaffUserId,
            new SessionCheckoutRequest(
                TestIds.OrganizationId,
                [new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", ExpectedTimeCharge))],
                "checkout-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(ExpectedTimeCharge, result.Response.TimeCharge.MinorUnits);
        Assert.Equal(0, result.Response.PosTotal.MinorUnits);
        Assert.Equal(ExpectedTimeCharge, result.Response.GrandTotal.MinorUnits);

        var session = await db.Sessions.SingleAsync();
        Assert.Equal(SessionStateNames.Ending, session.State);

        Assert.Equal(0, await DebtBalanceAsync(db)); // PostpaidDebt(+) then DebtPayment(-) net to zero.
        Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.PostpaidDebt));
        Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment));

        var payment = Assert.Single(db.Payments);
        Assert.Equal(SessionId, payment.SessionId);
        Assert.Null(payment.PosSaleId);
        Assert.Equal(PaymentMethodNames.Cash, payment.PaymentMethod);
        Assert.Equal(ExpectedTimeCharge, payment.AmountMinorUnits);

        var receipt = Assert.Single(db.Receipts);
        Assert.Equal(SessionId, receipt.SessionId);
        Assert.Null(receipt.PosSaleId);
        Assert.Equal(ExpectedTimeCharge, receipt.TotalMinorUnits);

        var lockCommand = Assert.Single(dispatcher.Enqueued);
        Assert.Equal(DeviceCommandTypeNames.Lock, lockCommand.Type);
        Assert.Single(dispatcher.Notified);
    }

    [Fact]
    public async Task CheckoutAsync_WithAttachedPosSale_UnifiesBillAndDecrementsStock()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedOpenPostpaidSessionAsync(db);
        await SeedAttachedPosSaleAsync(db, totalMinorUnits: 1000, quantity: 2);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);

        var result = await service.CheckoutAsync(
            SessionId,
            ActorStaffUserId,
            new SessionCheckoutRequest(
                TestIds.OrganizationId,
                [new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", ExpectedTimeCharge + 1000))],
                "checkout-pos-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Response);
        Assert.Equal(1000, result.Response.PosTotal.MinorUnits);
        Assert.Equal(ExpectedTimeCharge + 1000, result.Response.GrandTotal.MinorUnits);

        var sale = await db.PosSales.SingleAsync();
        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        Assert.Equal(Now, sale.PaidAtUtc);

        var stockOnHand = db.StockMovements
            .Where(movement => movement.ProductId == ProductId)
            .Sum(movement => movement.QuantityDelta);
        Assert.Equal(8, stockOnHand); // seeded 10, sold 2.

        var receipt = Assert.Single(db.Receipts);
        Assert.Equal(ExpectedTimeCharge + 1000, receipt.TotalMinorUnits);
    }

    [Fact]
    public async Task CheckoutAsync_SplitWalletAndCash_ConsumesWalletAndNetsDebt()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedWalletTopUpAsync(db, 5000);
        await SeedOpenPostpaidSessionAsync(db);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);

        var result = await service.CheckoutAsync(
            SessionId,
            ActorStaffUserId,
            new SessionCheckoutRequest(
                TestIds.OrganizationId,
                [
                    new PaymentPartDto(PaymentMethodNames.Wallet, new MoneyDto("TJS", 2000)),
                    new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", 250))
                ],
                "checkout-split-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(0, await DebtBalanceAsync(db));
        // Wallet: 5000 top-up - 2000 applied = 3000.
        Assert.Equal(3000, await WalletBalanceAsync(db));
        var walletPayment = Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment));
        Assert.Equal(-2000, walletPayment.AmountMinorUnits);
        Assert.Equal(2, db.Payments.Count());
    }

    [Fact]
    public async Task CheckoutAsync_WalletExceedsBalance_IsRejected()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedWalletTopUpAsync(db, 1000);
        await SeedOpenPostpaidSessionAsync(db);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);

        var result = await service.CheckoutAsync(
            SessionId,
            ActorStaffUserId,
            new SessionCheckoutRequest(
                TestIds.OrganizationId,
                [new PaymentPartDto(PaymentMethodNames.Wallet, new MoneyDto("TJS", ExpectedTimeCharge))],
                "checkout-wallet-over-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(dispatcher.Enqueued);
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(SessionStateNames.Active, session.State);
    }

    [Fact]
    public async Task CheckoutAsync_PaymentsDoNotSumToGrandTotal_IsRejected()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedOpenPostpaidSessionAsync(db);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);

        var result = await service.CheckoutAsync(
            SessionId,
            ActorStaffUserId,
            new SessionCheckoutRequest(
                TestIds.OrganizationId,
                [new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", 1000))],
                "checkout-underpay-001"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Empty(db.Payments);
        Assert.Empty(db.Receipts);
    }

    [Fact]
    public async Task CheckoutAsync_RepeatedWithSameKey_ReturnsOriginalWithoutDuplicates()
    {
        await using var db = CreateDbContext();
        await SeedCoreAsync(db);
        await SeedOpenPostpaidSessionAsync(db);
        var dispatcher = new RecordingDispatch();
        var service = CreateService(db, dispatcher);
        var request = new SessionCheckoutRequest(
            TestIds.OrganizationId,
            [new PaymentPartDto(PaymentMethodNames.Cash, new MoneyDto("TJS", ExpectedTimeCharge))],
            "checkout-idem-001");

        var first = await service.CheckoutAsync(SessionId, ActorStaffUserId, request, CancellationToken.None);
        var second = await service.CheckoutAsync(SessionId, ActorStaffUserId, request, CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.NotNull(first.Response);
        Assert.NotNull(second.Response);
        Assert.Equal(first.Response.SessionId, second.Response.SessionId);
        Assert.Single(db.Receipts);
        Assert.Single(db.Payments);
        Assert.Single(db.LedgerEntries.Where(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment));
    }

    private static async Task<long> DebtBalanceAsync(PlatformDbContext db) =>
        await db.LedgerEntries
            .Where(entry => entry.PlayerAccountId == PlayerAccountId && entry.AccountType == LedgerAccountTypeNames.Debt)
            .SumAsync(entry => (long?)entry.AmountMinorUnits) ?? 0;

    private static async Task<long> WalletBalanceAsync(PlatformDbContext db) =>
        await db.LedgerEntries
            .Where(entry => entry.PlayerAccountId == PlayerAccountId && entry.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => (long?)entry.AmountMinorUnits) ?? 0;

    private static PlatformDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new PlatformDbContext(options);
    }

    private static EfSessionCheckoutService CreateService(PlatformDbContext db, RecordingDispatch dispatcher)
    {
        var timeProvider = new FixedTimeProvider(Now);
        var shiftService = new EfShiftService(db, timeProvider);
        return new EfSessionCheckoutService(
            db,
            new SessionBillingService(db, new EfTariffService(db, timeProvider), shiftService, timeProvider),
            new ReceiptNumberGenerator(db),
            dispatcher,
            shiftService,
            timeProvider);
    }

    private static async Task SeedCoreAsync(PlatformDbContext db)
    {
        db.Organizations.Add(new OrganizationEntity
        {
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Org",
            CreatedAtUtc = Now
        });
        db.Branches.Add(new BranchEntity
        {
            BranchId = TestIds.BranchId,
            OrganizationId = TestIds.OrganizationId,
            Name = "Demo Branch",
            CreatedAtUtc = Now
        });
        db.Zones.Add(new ZoneEntity
        {
            ZoneId = ZoneId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Main Hall",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.Seats.Add(new SeatEntity
        {
            SeatId = SeatId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ZoneId = ZoneId,
            Name = "PC-001",
            SortOrder = 1,
            CreatedAtUtc = Now
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = PlayerAccountId,
            OrganizationId = TestIds.OrganizationId,
            HomeBranchId = TestIds.BranchId,
            DisplayName = "Player One",
            PhoneNumber = "+992000000001",
            IsActive = true,
            CreatedAtUtc = Now
        });
        var tariff = new TariffEntity
        {
            TariffId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            Name = "Standard",
            IsActive = true,
            CreatedAtUtc = Now
        };
        db.Tariffs.Add(tariff);
        db.TariffVersions.Add(new TariffVersionEntity
        {
            TariffVersionId = TariffVersionId,
            TariffId = tariff.TariffId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            VersionNumber = 1,
            CurrencyCode = "TJS",
            PricePerMinuteMinorUnits = 50,
            MinimumBillableMinutes = 30,
            RoundingIncrementMinutes = 15,
            EffectiveFromUtc = Now.AddDays(-1),
            CreatedAtUtc = Now
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            OpenedByStaffUserId = ActorStaffUserId,
            State = ShiftStateNames.Open,
            CurrencyCode = "TJS",
            StartingCashMinorUnits = 50000,
            OpeningNote = "test shift",
            ClosingNote = string.Empty,
            OpenedAtUtc = Now
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedOpenPostpaidSessionAsync(PlatformDbContext db)
    {
        db.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            SeatId = SeatId,
            DeviceId = TestIds.DeviceId,
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerKind = "guest",
            PlayerAccountId = PlayerAccountId,
            TariffRuleVersionId = TariffVersionId.ToString("D"),
            State = SessionStateNames.Active,
            RequestedAtUtc = Now.AddMinutes(-40),
            StartedAtUtc = Now.AddMinutes(-40),
            EndsAtUtc = null,
            UpdatedAtUtc = Now.AddMinutes(-40)
        });
        await db.SaveChangesAsync();
    }

    private static async Task SeedWalletTopUpAsync(PlatformDbContext db, long amountMinorUnits)
    {
        db.LedgerEntries.Add(BillingEntryFactory.Create(
            TestIds.OrganizationId,
            TestIds.BranchId,
            PlayerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            amountMinorUnits,
            quantitySeconds: 0,
            "TJS",
            LedgerEntryTypeNames.TopUp,
            "test wallet top-up",
            reversesLedgerEntryId: null,
            ActorStaffUserId,
            Now.AddMinutes(-50)));
        await db.SaveChangesAsync();
    }

    private static async Task SeedAttachedPosSaleAsync(PlatformDbContext db, long totalMinorUnits, int quantity)
    {
        var saleId = Guid.NewGuid();
        db.PosSales.Add(new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ShiftId = await db.Shifts.Select(shift => shift.ShiftId).FirstAsync(),
            CreatedByStaffUserId = ActorStaffUserId,
            PlayerAccountId = PlayerAccountId,
            SessionId = SessionId,
            State = PosSaleStateNames.Draft,
            CurrencyCode = "TJS",
            TotalMinorUnits = totalMinorUnits,
            CreatedAtUtc = Now.AddMinutes(-10)
        });
        db.PosSaleLines.Add(new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = saleId,
            ProductId = ProductId,
            ProductName = "Cola",
            Quantity = quantity,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = totalMinorUnits / quantity,
            LineTotalMinorUnits = totalMinorUnits,
            TrackStock = true,
            AllowNegativeStock = false
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(),
            OrganizationId = TestIds.OrganizationId,
            BranchId = TestIds.BranchId,
            ProductId = ProductId,
            MovementType = StockMovementTypeNames.Purchase,
            QuantityDelta = 10,
            CurrencyCode = "TJS",
            UnitCostMinorUnits = 0,
            Reason = "seed stock",
            CreatedByStaffUserId = ActorStaffUserId,
            CreatedAtUtc = Now.AddMinutes(-60)
        });
        await db.SaveChangesAsync();
    }

    private sealed class RecordingDispatch : IDeviceCommandDispatchService
    {
        public List<DeviceCommandDto> Enqueued { get; } = [];

        public List<DeviceCommandDto> Notified { get; } = [];

        public Task<DeviceCommandDto> EnqueueAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken)
        {
            var command = new DeviceCommandDto(Guid.NewGuid(), request.Type, DateTimeOffset.UnixEpoch, request.Payload);
            Enqueued.Add(command);
            return Task.FromResult(command);
        }

        public Task NotifyAsync(Guid deviceId, DeviceCommandDto command, CancellationToken cancellationToken)
        {
            Notified.Add(command);
            return Task.CompletedTask;
        }

        public Task<DeviceCommandDto> DispatchAsync(Guid deviceId, CreateDeviceCommandRequest request, CancellationToken cancellationToken)
        {
            return EnqueueAsync(deviceId, request, cancellationToken);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
