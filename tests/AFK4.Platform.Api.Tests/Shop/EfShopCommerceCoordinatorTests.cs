using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Commerce;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Inventory;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Receipts;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopCommerceCoordinatorTests
{
    private static readonly Guid OrganizationId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid BranchId = Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid PlayerAccountId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid SeatId = Guid.Parse("55555555-5555-4555-8555-555555555555");
    private static readonly Guid StaffUserId = Guid.Parse("66666666-6666-4666-8666-666666666666");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-13T12:00:00Z");

    [Fact]
    public async Task PlaceAsync_CreatesOneLinkedPaidSaleAndOneOrder()
    {
        await using var db = NewDb();
        var product = await SeedAsync(db);
        var notifier = new RecordingNotifier(db);
        var service = CreateService(db, notifier);

        var result = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 3)], "place-001"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        var order = Assert.Single(db.ShopOrders);
        var sale = Assert.Single(db.PosSales);
        Assert.Equal(sale.PosSaleId, order.PosSaleId);
        Assert.Equal(sale.PosSaleId, result.Order!.PosSaleId);
        Assert.Equal(PosSaleStateNames.Paid, sale.State);
        Assert.Single(db.Payments);
        Assert.Single(db.Receipts);
        Assert.Single(db.PosSaleLines);
        Assert.Single(db.ShopOrderLines);
        Assert.Equal(2, await db.LedgerEntries.CountAsync());
        Assert.Equal(2, await db.StockMovements.CountAsync());
        Assert.True(notifier.CreatedObservedPersistedOrder);
        Assert.Equal(1, notifier.CreatedCount);
    }

    [Theory]
    [InlineData("missing_shift", "open_shift_required")]
    [InlineData("insufficient_funds", "insufficient_funds")]
    [InlineData("out_of_stock", "out_of_stock")]
    [InlineData("unavailable_product", "product_unavailable")]
    public async Task PlaceAsync_Rejection_LeavesNoRecords(string scenario, string errorCode)
    {
        await using var db = NewDb();
        var product = await SeedAsync(
            db,
            openShift: scenario != "missing_shift",
            walletMinorUnits: scenario == "insufficient_funds" ? 100 : 10_000,
            stock: scenario == "out_of_stock" ? 1 : 10,
            availableInShell: scenario != "unavailable_product");
        var baselineLedger = await db.LedgerEntries.CountAsync();
        var baselineStock = await db.StockMovements.CountAsync();

        var result = await CreateService(db, new RecordingNotifier(db)).PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 3)], $"place-{scenario}"),
            CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(errorCode, result.ErrorCode);
        Assert.Empty(db.ShopOrders);
        Assert.Empty(db.PosSales);
        Assert.Empty(db.Payments);
        Assert.Empty(db.Receipts);
        Assert.Equal(baselineLedger, await db.LedgerEntries.CountAsync());
        Assert.Equal(baselineStock, await db.StockMovements.CountAsync());
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Added);
    }

    [Fact]
    public async Task PlaceAsync_ReplayWithCanonicalLines_ReturnsOriginalOrder()
    {
        await using var db = NewDb();
        var firstProduct = await SeedAsync(db);
        var secondProduct = await AddProductAsync(db, "Chips", 700, stock: 10);
        var service = CreateService(db, new RecordingNotifier(db));

        var first = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest(
                [
                    new ShopOrderLineInput(secondProduct.ProductId, 1),
                    new ShopOrderLineInput(firstProduct.ProductId, 1),
                    new ShopOrderLineInput(firstProduct.ProductId, 2)
                ],
                "place-canonical"),
            CancellationToken.None);
        var replay = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest(
                [
                    new ShopOrderLineInput(firstProduct.ProductId, 3),
                    new ShopOrderLineInput(secondProduct.ProductId, 1)
                ],
                "place-canonical"),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(replay.Succeeded);
        Assert.Equivalent(first.Order, replay.Order, strict: true);
        Assert.Single(db.ShopOrders);
        Assert.Single(db.PosSales);
        Assert.Single(db.Payments);
        Assert.Single(db.Receipts);
        Assert.Single(db.BillingCommandIdempotency);
    }

    [Fact]
    public async Task PlaceAsync_ReusedKeyWithChangedLines_ReturnsConflict()
    {
        await using var db = NewDb();
        var product = await SeedAsync(db);
        var service = CreateService(db, new RecordingNotifier(db));

        var first = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 1)], "place-conflict"),
            CancellationToken.None);
        var conflict = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 2)], "place-conflict"),
            CancellationToken.None);

        Assert.True(first.Succeeded);
        Assert.True(conflict.Conflict);
        Assert.Equal("idempotency_conflict", conflict.ErrorCode);
        Assert.Single(db.ShopOrders);
        Assert.Single(db.PosSales);
    }

    [Fact]
    public async Task PlaceAsync_ReplayAfterSessionEnds_ReturnsOriginalOrder()
    {
        await using var db = NewDb();
        var product = await SeedAsync(db);
        var service = CreateService(db, new RecordingNotifier(db));
        var request = new PlaceShopOrderRequest(
            [new ShopOrderLineInput(product.ProductId, 1)],
            "place-ended-session-replay");
        var first = await service.PlaceAsync(PlayerAccountId, request, CancellationToken.None);
        var session = await db.Sessions.SingleAsync();
        session.State = SessionStateNames.Ended;
        await db.SaveChangesAsync();

        var replay = await service.PlaceAsync(PlayerAccountId, request, CancellationToken.None);

        Assert.True(replay.Succeeded);
        Assert.Equal(first.Order!.Id, replay.Order!.Id);
        Assert.Single(db.ShopOrders);
        Assert.Single(db.PosSales);
    }

    [Fact]
    public async Task PlaceAsync_NotifierFailure_DoesNotChangeCommittedSuccess()
    {
        await using var db = NewDb();
        var product = await SeedAsync(db);

        var result = await CreateService(db, new ThrowingNotifier()).PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 1)], "place-notify-failure"),
            CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Single(db.ShopOrders);
        Assert.Single(db.PosSales);
    }

    [Fact]
    public async Task RefundLinkedSaleAsync_ReplayReturnsSameSale_ChangedReasonConflicts()
    {
        await using var db = NewDb();
        var product = await SeedAsync(db);
        var notifier = new RecordingNotifier(db);
        var service = CreateService(db, notifier);
        var placed = await service.PlaceAsync(
            PlayerAccountId,
            new PlaceShopOrderRequest([new ShopOrderLineInput(product.ProductId, 1)], "place-refund"),
            CancellationToken.None);
        var saleId = placed.Order!.PosSaleId!.Value;

        var first = await service.RefundLinkedSaleAsync(
            saleId,
            StaffUserId,
            new RefundPosSaleRequest(OrganizationId, "changed mind", "refund-001"),
            CancellationToken.None);

        var cancelled = await db.ShopOrders.AsNoTracking().SingleAsync();
        Assert.True(first.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Cancelled, cancelled.Status);
        Assert.Equal(placed.Order.Version + 1, cancelled.Version);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Equal(1, notifier.UpdatedCount);
        Assert.True(notifier.UpdatedObservedPersistedCancellation);

        var paymentCount = await db.Payments.CountAsync();
        var receiptCount = await db.Receipts.CountAsync();
        var ledgerCount = await db.LedgerEntries.CountAsync();
        var movementCount = await db.StockMovements.CountAsync();
        var replay = await service.RefundLinkedSaleAsync(
            saleId,
            StaffUserId,
            new RefundPosSaleRequest(OrganizationId, "changed mind", "refund-001"),
            CancellationToken.None);
        var replayedOrder = await db.ShopOrders.AsNoTracking().SingleAsync();

        Assert.True(replay.Succeeded);
        Assert.Equivalent(first.Response, replay.Response, strict: true);
        Assert.Equal(cancelled.Version, replayedOrder.Version);
        Assert.Equal(cancelled.CancelledAtUtc, replayedOrder.CancelledAtUtc);
        Assert.Equal(1, notifier.UpdatedCount);
        Assert.Equal(paymentCount, await db.Payments.CountAsync());
        Assert.Equal(receiptCount, await db.Receipts.CountAsync());
        Assert.Equal(ledgerCount, await db.LedgerEntries.CountAsync());
        Assert.Equal(movementCount, await db.StockMovements.CountAsync());

        var conflict = await service.RefundLinkedSaleAsync(
            saleId,
            StaffUserId,
            new RefundPosSaleRequest(OrganizationId, "different", "refund-001"),
            CancellationToken.None);

        Assert.True(conflict.Conflict);
        Assert.Equal("idempotency_conflict", conflict.Error);
        Assert.Equal(1, notifier.UpdatedCount);
        Assert.Equal(paymentCount, await db.Payments.CountAsync());
        Assert.Equal(receiptCount, await db.Receipts.CountAsync());
        Assert.Equal(ledgerCount, await db.LedgerEntries.CountAsync());
        Assert.Equal(movementCount, await db.StockMovements.CountAsync());
    }

    private static EfShopCommerceCoordinator CreateService(PlatformDbContext db, IShopOrderNotifier notifier)
    {
        var workflow = new EfShopOrderWorkflow(
            db,
            TimeProvider.System,
            notifier,
            new LoyaltyAccrualService(db));
        var settlement = new EfShopPosSettlementService(
            db,
            new EfWalletSettlementService(db),
            new EfInventoryCostService(db),
            new ReceiptNumberGenerator(db));
        return new EfShopCommerceCoordinator(
            db,
            workflow,
            settlement,
            TimeProvider.System,
            notifier,
            NullLogger<EfShopCommerceCoordinator>.Instance);
    }

    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

    private static async Task<PosProductEntity> SeedAsync(
        PlatformDbContext db,
        bool openShift = true,
        long walletMinorUnits = 10_000,
        int stock = 10,
        bool availableInShell = true)
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
            DisplayName = "Alex",
            IsActive = true,
            CreatedAtUtc = Now
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = SessionId,
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            SeatId = SeatId,
            PlayerAccountId = PlayerAccountId,
            State = SessionStateNames.Active,
            PlayerKind = "registered",
            TariffRuleVersionId = "v1",
            Version = 1
        });
        if (openShift)
        {
            db.Shifts.Add(new ShiftEntity
            {
                ShiftId = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                OpenedByStaffUserId = StaffUserId,
                State = ShiftStateNames.Open,
                CurrencyCode = "TJS",
                OpenedAtUtc = Now.AddHours(-1)
            });
        }

        db.LedgerEntries.Add(BillingEntryFactory.Create(
            OrganizationId,
            BranchId,
            PlayerAccountId,
            null,
            null,
            LedgerEntryTypeNames.TopUp,
            LedgerAccountTypeNames.Wallet,
            walletMinorUnits,
            0,
            "TJS",
            "seed",
            "seed",
            null,
            StaffUserId,
            Now.AddMinutes(-10)));
        await db.SaveChangesAsync();
        return await AddProductAsync(db, "Cola", 500, stock, availableInShell);
    }

    private static async Task<PosProductEntity> AddProductAsync(
        PlatformDbContext db,
        string name,
        long price,
        int stock,
        bool availableInShell = true)
    {
        var product = new PosProductEntity
        {
            ProductId = Guid.NewGuid(),
            OrganizationId = OrganizationId,
            BranchId = BranchId,
            CategoryId = Guid.NewGuid(),
            Name = name,
            Sku = $"SKU-{Guid.NewGuid():N}",
            CurrencyCode = "TJS",
            PriceMinorUnits = price,
            AvgCostMinorUnits = 100,
            TrackStock = true,
            AllowNegativeStock = false,
            IsActive = true,
            AvailableInShell = availableInShell,
            CreatedAtUtc = Now
        };
        db.PosProducts.Add(product);
        if (stock != 0)
        {
            db.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = OrganizationId,
                BranchId = BranchId,
                ProductId = product.ProductId,
                MovementType = StockMovementTypeNames.Purchase,
                QuantityDelta = stock,
                CurrencyCode = "TJS",
                UnitCostMinorUnits = 100,
                Reason = "seed",
                CreatedByStaffUserId = StaffUserId,
                CreatedAtUtc = Now.AddMinutes(-5)
            });
        }
        await db.SaveChangesAsync();
        return product;
    }

    private sealed class RecordingNotifier(PlatformDbContext db) : IShopOrderNotifier
    {
        public int CreatedCount { get; private set; }
        public bool CreatedObservedPersistedOrder { get; private set; }
        public int UpdatedCount { get; private set; }
        public bool UpdatedObservedPersistedCancellation { get; private set; }

        public async Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken)
        {
            CreatedCount++;
            CreatedObservedPersistedOrder = await db.ShopOrders.AsNoTracking()
                .AnyAsync(candidate => candidate.ShopOrderId == order.Id, cancellationToken);
        }

        public async Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken)
        {
            UpdatedCount++;
            UpdatedObservedPersistedCancellation = await db.ShopOrders.AsNoTracking()
                .AnyAsync(candidate =>
                    candidate.ShopOrderId == order.Id &&
                    candidate.Status == ShopOrderStatusNames.Cancelled,
                    cancellationToken);
        }
    }

    private sealed class ThrowingNotifier : IShopOrderNotifier
    {
        public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("realtime unavailable");

        public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
