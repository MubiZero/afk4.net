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
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopOrderServiceTransitionTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static PlatformDbContext NewDb(SaveChangesInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .AddInterceptors(interceptor)
            .Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Staff = Guid.NewGuid();
    private static readonly Guid Product = Guid.NewGuid();
    private static readonly Guid Seat = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static EfShopOrderService NewService(PlatformDbContext db, IShopOrderNotifier? notifierOverride = null)
    {
        var notifier = notifierOverride ?? new NoopShopOrderNotifier();
        var workflow = new EfShopOrderWorkflow(db, TimeProvider.System, notifier, new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance));
        var settlement = new EfShopPosSettlementService(
            db, new EfWalletSettlementService(db), new EfInventoryCostService(db), new ReceiptNumberGenerator(db));
        var coordinator = new EfShopCommerceCoordinator(
            db, workflow, settlement, TimeProvider.System, notifier, NullLogger<EfShopCommerceCoordinator>.Instance);
        return new EfShopOrderService(coordinator, workflow);
    }

    private static async Task<ShopOrderDto> SeedPlacedOrderAsync(PlatformDbContext db)
    {
        db.Branches.Add(new BranchEntity
        {
            BranchId = Branch, OrganizationId = Org, Name = "Central", PreferredLocale = "ru",
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = Player, OrganizationId = Org, HomeBranchId = Branch,
            DisplayName = "Alex", IsActive = true, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.Sessions.Add(new SessionEntity
        {
            SessionId = Session, OrganizationId = Org, BranchId = Branch, SeatId = Seat,
            PlayerAccountId = Player, State = "active",
            PlayerKind = "registered", TariffRuleVersionId = "v1", Version = 1
        });
        db.PosProducts.Add(new PosProductEntity
        {
            ProductId = Product, OrganizationId = Org, BranchId = Branch, CategoryId = Guid.NewGuid(),
            Name = "Cola", Sku = "COLA", CurrencyCode = "TJS", PriceMinorUnits = 500,
            TrackStock = true, AllowNegativeStock = false, IsActive = true, AvailableInShell = true,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch,
            OpenedByStaffUserId = Staff, State = AFK4.Shared.Contracts.Shifts.ShiftStateNames.Open,
            CurrencyCode = "TJS", OpenedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.StockMovements.Add(new StockMovementEntity
        {
            StockMovementId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch, ProductId = Product,
            MovementType = StockMovementTypeNames.Purchase, QuantityDelta = 10,
            CurrencyCode = "TJS", UnitCostMinorUnits = 0, Reason = "seed",
            CreatedByStaffUserId = Guid.Empty, CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.LedgerEntries.Add(BillingEntryFactory.Create(
            Org, Branch, Player, null, null, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
            5000, 0, "TJS", "seed", "seed", null, Guid.Empty, DateTimeOffset.UnixEpoch));
        await db.SaveChangesAsync();

        var placed = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(Product, 3)], $"place-{Guid.NewGuid():N}"), CancellationToken.None);
        return placed.Order!;
    }

    [Fact]
    public async Task Accept_ThenDeliver_AdvancesStatus()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);
        var paymentCount = await db.Payments.CountAsync();
        var receiptCount = await db.Receipts.CountAsync();
        var stockCount = await db.StockMovements.CountAsync();
        var walletPaymentCount = await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment);

        var accepted = await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);
        Assert.True(accepted.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Accepted, accepted.Order!.Status);

        var delivered = await service.DeliverAsync(Branch, order.Id, Staff, accepted.Order.Version, CancellationToken.None);
        Assert.True(delivered.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Delivered, delivered.Order!.Status);
        Assert.NotNull(delivered.Order.DeliveredAtUtc);
        Assert.Equal(paymentCount, await db.Payments.CountAsync());
        Assert.Equal(receiptCount, await db.Receipts.CountAsync());
        Assert.Equal(stockCount, await db.StockMovements.CountAsync());
        Assert.Equal(walletPaymentCount, await db.LedgerEntries.CountAsync(entry => entry.EntryType == LedgerEntryTypeNames.WalletPayment));
    }

    [Fact]
    public async Task DeliverAsync_AccruesShopCashbackWhenEnabled()
    {
        await using var db = NewDb();
        db.OrganizationLoyaltySettings.Add(new OrganizationLoyaltySettingsEntity
        {
            OrganizationId = Org,
            TopUpEnabled = false,
            TopUpPercentBasisPoints = 0,
            ShopEnabled = true,
            ShopPercentBasisPoints = 300,
            UpdatedAtUtc = DateTimeOffset.UnixEpoch
        });
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var accepted = await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);
        var delivered = await service.DeliverAsync(Branch, order.Id, Staff, accepted.Order!.Version, CancellationToken.None);

        Assert.True(delivered.Succeeded);
        var cashback = await db.LedgerEntries.SingleAsync(e => e.EntryType == LedgerEntryTypeNames.Cashback);
        Assert.Equal(order.Total.MinorUnits * 300 / 10000, cashback.AmountMinorUnits);
        Assert.Equal(LedgerAccountTypeNames.Wallet, cashback.AccountType);
    }

    [Fact]
    public async Task DeliverAsync_AccruesNoCashbackWhenNoOrgSettings()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var accepted = await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);
        var delivered = await service.DeliverAsync(Branch, order.Id, Staff, accepted.Order!.Version, CancellationToken.None);

        Assert.True(delivered.Succeeded);
        Assert.Empty(db.LedgerEntries.Where(e => e.EntryType == LedgerEntryTypeNames.Cashback));
    }

    [Fact]
    public async Task Accept_WithStaleVersion_ReturnsConflict()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var result = await service.AcceptAsync(Branch, order.Id, Staff, expectedVersion: 99, CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal(order.Version, result.CurrentVersion);
    }

    [Fact]
    public async Task Accept_SaveConcurrencyFailure_ReturnsCurrentVersionAndClearsMutation()
    {
        var interceptor = new ThrowConcurrencyOnSaveInterceptor();
        await using var db = NewDb(interceptor);
        var order = await SeedPlacedOrderAsync(db);
        interceptor.Armed = true;

        var result = await NewService(db).AcceptAsync(
            Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.True(result.Conflict);
        Assert.Equal("version_conflict", result.ErrorCode);
        Assert.Equal(order.Version, result.CurrentVersion);
        Assert.Equal(ShopOrderStatusNames.Placed, (await db.ShopOrders.AsNoTracking().SingleAsync()).Status);
        Assert.DoesNotContain(db.ChangeTracker.Entries(), entry => entry.State == EntityState.Modified);
    }

    [Fact]
    public async Task AcceptAsync_NotifierFailure_DoesNotChangeCommittedSuccess()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);

        var result = await NewService(db, new ThrowingUpdateNotifier()).AcceptAsync(
            Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Accepted, (await db.ShopOrders.SingleAsync()).Status);
    }

    [Fact]
    public async Task Deliver_WhenNotAccepted_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);

        var result = await NewService(db).DeliverAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    [Fact]
    public async Task CancelByOperator_RefundsWalletAndRestoresStock()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);

        var result = await NewService(db).CancelByOperatorAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Cancelled, result.Order!.Status);

        var wallet = await db.LedgerEntries.Where(e => e.PlayerAccountId == Player && e.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(e => e.AmountMinorUnits);
        Assert.Equal(5000, wallet);

        var onHand = await db.StockMovements.Where(m => m.ProductId == Product).SumAsync(m => m.QuantityDelta);
        Assert.Equal(10, onHand);

        var payments = await db.Payments.CountAsync();
        var receipts = await db.Receipts.CountAsync();
        var ledger = await db.LedgerEntries.CountAsync();
        var movements = await db.StockMovements.CountAsync();
        var repeated = await NewService(db).CancelByOperatorAsync(
            Branch, order.Id, Staff, expectedVersion: order.Version, CancellationToken.None);

        Assert.True(repeated.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Cancelled, repeated.Order!.Status);
        Assert.Equal(payments, await db.Payments.CountAsync());
        Assert.Equal(receipts, await db.Receipts.CountAsync());
        Assert.Equal(ledger, await db.LedgerEntries.CountAsync());
        Assert.Equal(movements, await db.StockMovements.CountAsync());
    }

    [Fact]
    public async Task CancelByOperator_LegacyOrderWithoutLinkedSale_UsesLegacyReversal()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var entity = await db.ShopOrders.SingleAsync();
        entity.PosSaleId = null;
        db.PosSaleLines.RemoveRange(db.PosSaleLines);
        db.Payments.RemoveRange(db.Payments);
        db.Receipts.RemoveRange(db.Receipts);
        db.PosSales.RemoveRange(db.PosSales);
        await db.SaveChangesAsync();

        var result = await NewService(db).CancelByOperatorAsync(
            Branch, order.Id, Staff, order.Version, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Cancelled, result.Order!.Status);
        Assert.Equal(5000, await db.LedgerEntries
            .Where(entry => entry.PlayerAccountId == Player && entry.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(entry => entry.AmountMinorUnits));
        Assert.Equal(10, await db.StockMovements
            .Where(movement => movement.ProductId == Product)
            .SumAsync(movement => movement.QuantityDelta));
    }

    [Fact]
    public async Task CancelByPlayer_AfterAccepted_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);
        await service.AcceptAsync(Branch, order.Id, Staff, order.Version, CancellationToken.None);

        var result = await service.CancelByPlayerAsync(Player, order.Id, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("invalid_transition", result.ErrorCode);
    }

    [Fact]
    public async Task ListForPlayer_And_ListQueue_ReturnOrders()
    {
        await using var db = NewDb();
        var order = await SeedPlacedOrderAsync(db);
        var service = NewService(db);

        var mine = await service.ListForPlayerAsync(Player, CancellationToken.None);
        Assert.Contains(mine, o => o.Id == order.Id);

        var queue = await service.ListQueueAsync(Branch, CancellationToken.None);
        Assert.Contains(queue, o => o.Id == order.Id);
    }
}

internal sealed class ThrowConcurrencyOnSaveInterceptor : SaveChangesInterceptor
{
    public bool Armed { get; set; }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default) =>
        Armed
            ? ValueTask.FromException<InterceptionResult<int>>(
                new DbUpdateConcurrencyException("deterministic shop transition conflict"))
            : ValueTask.FromResult(result);
}

internal sealed class ThrowingUpdateNotifier : IShopOrderNotifier
{
    public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("realtime unavailable");
}
