using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Shop;
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
            new PlaceShopOrderRequest(new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) }));
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
            new PlaceShopOrderRequest(new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) }));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ShopErrorBody>();
        Assert.Equal("insufficient_funds", body!.Error);
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
}
