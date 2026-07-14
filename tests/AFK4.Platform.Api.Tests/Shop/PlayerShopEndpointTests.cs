using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class PlayerShopEndpointTests
{
    [Fact]
    public async Task GetCatalog_ReturnsOnlyShellAvailableProducts()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var catalog = await client.GetFromJsonAsync<List<ShopCatalogItemDto>>("/api/me/shop/catalog");

        Assert.NotNull(catalog);
        Assert.Single(catalog!);
        Assert.Equal("Cola", catalog![0].Name);
        Assert.Equal(10, catalog[0].StockOnHand);
    }

    [Fact]
    public async Task PlaceOrder_DebitsWalletAndReturnsPlaced()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(
                new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) },
                "player-shop-place-001"));
        var order = await response.Content.ReadFromJsonAsync<ShopOrderDto>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ShopOrderStatusNames.Placed, order!.Status);
        Assert.Equal(1500, order.Total.MinorUnits);
    }

    [Fact]
    public async Task PlaceOrder_WithInsufficientFunds_Returns409WithCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory, walletMinor: 1000);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(
                new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) },
                "player-shop-insufficient-001"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShopErrorBody>();
        Assert.Equal("insufficient_funds", body!.Error);
    }

    [Fact]
    public async Task PlaceOrder_WithoutOpenShift_Returns409OpenShiftRequired()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            db.Shifts.RemoveRange(db.Shifts.Where(shift => shift.BranchId == seeded.BranchId));
            await db.SaveChangesAsync();
        }
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(
                [new ShopOrderLineInput(seeded.ColaProductId, 1)],
                "place-no-shift"));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("open_shift_required", (await response.Content.ReadFromJsonAsync<ShopErrorBody>())!.Error);
    }

    [Fact]
    public async Task PlaceOrder_EmptyKey_ReturnsIdempotencyKeyRequired()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);

        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 1)], ""));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("idempotency_key_required", (await response.Content.ReadFromJsonAsync<ShopErrorBody>())!.Error);
    }

    [Fact]
    public async Task PlaceOrder_ReusedKeyWithChangedLines_ReturnsIdempotencyConflict()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);
        const string key = "player-shop-conflict-001";

        (await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 1)], key)))
            .EnsureSuccessStatusCode();
        var response = await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 2)], key));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("idempotency_conflict", (await response.Content.ReadFromJsonAsync<ShopErrorBody>())!.Error);
    }

    [Fact]
    public async Task CancelOrder_ByPlayer_RefundsLinkedSale()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(client, seeded);
        var placed = await (await client.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 2)], "player-cancel-001")))
            .Content.ReadFromJsonAsync<ShopOrderDto>();

        var first = await client.PostAsync($"/api/me/shop/orders/{placed!.Id:D}/cancel", null);
        var second = await client.PostAsync($"/api/me/shop/orders/{placed.Id:D}/cancel", null);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var cancelled = await first.Content.ReadFromJsonAsync<ShopOrderDto>();
        Assert.Equal(ShopOrderStatusNames.Cancelled, cancelled!.Status);
        await AssertLinkedRefundStateAsync(factory, placed.Id, placed.PosSaleId!.Value);
    }

    [Fact]
    public async Task GetCatalog_Unauthenticated_Returns401()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/me/shop/catalog");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record ShopErrorBody(string Error);

    internal static async Task AssertLinkedRefundStateAsync(
        PlatformApiFactory factory,
        Guid orderId,
        Guid saleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var order = await db.ShopOrders.SingleAsync(candidate => candidate.ShopOrderId == orderId);
        Assert.Equal(ShopOrderStatusNames.Cancelled, order.Status);
        Assert.Equal(PosSaleStateNames.Refunded,
            await db.PosSales.Where(sale => sale.PosSaleId == saleId).Select(sale => sale.State).SingleAsync());
        Assert.Equal(2, await db.Payments.CountAsync(payment => payment.PosSaleId == saleId));
        Assert.Equal(2, await db.Receipts.CountAsync(receipt => receipt.PosSaleId == saleId));
        Assert.Equal(2, await db.LedgerEntries.CountAsync(entry =>
            entry.LedgerEntryId == order.WalletLedgerEntryId ||
            entry.ReversesLedgerEntryId == order.WalletLedgerEntryId));
        Assert.Equal(1, await db.StockMovements.CountAsync(movement => movement.MovementType == StockMovementTypeNames.Sale));
        Assert.Equal(1, await db.StockMovements.CountAsync(movement => movement.MovementType == StockMovementTypeNames.Refund));
    }
}
