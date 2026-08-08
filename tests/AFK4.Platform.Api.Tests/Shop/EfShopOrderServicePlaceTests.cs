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
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class EfShopOrderServicePlaceTests
{
    private static PlatformDbContext NewDb() =>
        new(new DbContextOptionsBuilder<PlatformDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N")).Options);

    private static readonly Guid Org = Guid.NewGuid();
    private static readonly Guid Branch = Guid.NewGuid();
    private static readonly Guid Player = Guid.NewGuid();
    private static readonly Guid Seat = Guid.NewGuid();
    private static readonly Guid Session = Guid.NewGuid();

    private static async Task SeedAsync(PlatformDbContext db, Guid productId, long walletMinor, int stock, bool availableInShell = true)
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
            ProductId = productId, OrganizationId = Org, BranchId = Branch, CategoryId = Guid.NewGuid(),
            Name = "Cola", Sku = "COLA", CurrencyCode = "TJS", PriceMinorUnits = 500,
            TrackStock = true, AllowNegativeStock = false, IsActive = true, AvailableInShell = availableInShell,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        });
        db.Shifts.Add(new ShiftEntity
        {
            ShiftId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch,
            OpenedByStaffUserId = Guid.Empty, State = AFK4.Shared.Contracts.Shifts.ShiftStateNames.Open,
            CurrencyCode = "TJS", OpenedAtUtc = DateTimeOffset.UnixEpoch
        });
        if (stock != 0)
        {
            db.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(), OrganizationId = Org, BranchId = Branch, ProductId = productId,
                MovementType = StockMovementTypeNames.Purchase, QuantityDelta = stock,
                CurrencyCode = "TJS", UnitCostMinorUnits = 0, Reason = "seed",
                CreatedByStaffUserId = Guid.Empty, CreatedAtUtc = DateTimeOffset.UnixEpoch
            });
        }
        if (walletMinor != 0)
        {
            db.LedgerEntries.Add(BillingEntryFactory.Create(
                Org, Branch, Player, null, null, LedgerEntryTypeNames.TopUp, LedgerAccountTypeNames.Wallet,
                walletMinor, 0, "TJS", "seed", "seed", null, Guid.Empty, DateTimeOffset.UnixEpoch));
        }
        await db.SaveChangesAsync();
    }

    private static EfShopOrderService NewService(PlatformDbContext db)
    {
        var notifier = new NoopShopOrderNotifier();
        var workflow = new EfShopOrderWorkflow(db, TimeProvider.System, notifier, new LoyaltyAccrualService(db, AlwaysEnabledOrganizationEntitlements.Instance));
        var settlement = new EfShopPosSettlementService(
            db, new EfWalletSettlementService(db), new EfInventoryCostService(db), new ReceiptNumberGenerator(db));
        var coordinator = new EfShopCommerceCoordinator(
            db, workflow, settlement, TimeProvider.System, notifier, NullLogger<EfShopCommerceCoordinator>.Instance);
        return new EfShopOrderService(coordinator, workflow);
    }

    [Fact]
    public async Task Place_DebitsWalletDecrementsStockAndCreatesOrder()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10);

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 3)], "place-debit"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(ShopOrderStatusNames.Placed, result.Order!.Status);
        Assert.Equal(1500, result.Order.Total.MinorUnits);
        Assert.Equal(Seat, result.Order.SeatId);

        var wallet = await db.LedgerEntries.Where(e => e.PlayerAccountId == Player && e.AccountType == LedgerAccountTypeNames.Wallet)
            .SumAsync(e => e.AmountMinorUnits);
        Assert.Equal(3500, wallet);

        var onHand = await db.StockMovements.Where(m => m.ProductId == productId).SumAsync(m => m.QuantityDelta);
        Assert.Equal(7, onHand);
    }

    [Fact]
    public async Task Place_WithInsufficientFunds_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 1000, stock: 10);

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 3)], "place-funds"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("insufficient_funds", result.ErrorCode);
        Assert.Empty(db.ShopOrders);
    }

    [Fact]
    public async Task Place_WithInsufficientStock_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 2);

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 3)], "place-stock"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("out_of_stock", result.ErrorCode);
    }

    [Fact]
    public async Task Place_WithUnavailableProduct_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10, availableInShell: false);

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 1)], "place-unavailable"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("product_unavailable", result.ErrorCode);
    }

    [Fact]
    public async Task Place_WithPausedSession_Succeeds()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10);
        var session = await db.Sessions.SingleAsync();
        session.State = "paused";
        await db.SaveChangesAsync();

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 1)], "place-paused"), CancellationToken.None);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Place_WithoutActiveSession_ReturnsBusinessError()
    {
        await using var db = NewDb();
        var productId = Guid.NewGuid();
        await SeedAsync(db, productId, walletMinor: 5000, stock: 10);
        var session = await db.Sessions.SingleAsync();
        session.State = "ended";
        await db.SaveChangesAsync();

        var result = await NewService(db).PlaceAsync(
            Player, new PlaceShopOrderRequest([new ShopOrderLineInput(productId, 1)], "place-no-session"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal("no_active_session", result.ErrorCode);
    }
}

internal sealed class NoopShopOrderNotifier : IShopOrderNotifier
{
    public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) => Task.CompletedTask;
}
