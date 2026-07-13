using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopCommercePostgresTests
{
    [Fact]
    public async Task Fixture_NonTestDatabase_IsRejectedBeforeConnection()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            ShopCommercePostgresFixture.CreateAsync(
                "Host=127.0.0.1;Port=1;Database=afk4_production;Username=postgres;Timeout=1"));

        Assert.Contains("ending in _test", exception.Message, StringComparison.Ordinal);
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

        Assert.Single(results, result => result.Succeeded);
        Assert.Single(results, result => result.ErrorCode == "out_of_stock");

        await using var verificationDb = database.CreateDbContext();
        var order = Assert.Single(await verificationDb.ShopOrders.AsNoTracking().ToListAsync());
        var sale = Assert.Single(await verificationDb.PosSales.AsNoTracking().ToListAsync());
        Assert.Equal(sale.PosSaleId, order.PosSaleId);
        Assert.Single(await verificationDb.PosSaleLines.AsNoTracking().ToListAsync());
        Assert.Single(await verificationDb.Payments.AsNoTracking().ToListAsync());
        Assert.Single(await verificationDb.Receipts.AsNoTracking().ToListAsync());
        Assert.Equal(2, await verificationDb.LedgerEntries.AsNoTracking().CountAsync());
        Assert.Equal(2, await verificationDb.StockMovements.AsNoTracking().CountAsync());
        Assert.Single(await verificationDb.BillingCommandIdempotency.AsNoTracking().ToListAsync());
    }
}
