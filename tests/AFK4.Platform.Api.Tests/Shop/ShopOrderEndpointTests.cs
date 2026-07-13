using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Shop;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopOrderEndpointTests
{
    [Fact]
    public async Task Queue_Accept_Deliver_Flow_WithPermission()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        using var playerClient = factory.CreateClient();

        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(playerClient, seeded);
        var placed = await (await playerClient.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest(
                new[] { new ShopOrderLineInput(seeded.ColaProductId, 3) },
                "shop-order-flow-001")))
            .Content.ReadFromJsonAsync<ShopOrderDto>();

        await ShopTestSeed.AuthorizeStaffForBranchAsync(factory, staffClient, seeded.OrganizationId, seeded.BranchId, withShopPermission: true);

        var queue = await staffClient.GetFromJsonAsync<List<ShopOrderDto>>($"/api/branches/{seeded.BranchId:D}/shop/orders");
        Assert.Contains(queue!, o => o.Id == placed!.Id);

        var accept = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed!.Id:D}/accept",
            new { expectedVersion = placed.Version });
        Assert.Equal(HttpStatusCode.OK, accept.StatusCode);
        var accepted = await accept.Content.ReadFromJsonAsync<ShopOrderDto>();

        var deliver = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed.Id:D}/deliver",
            new { expectedVersion = accepted!.Version });
        Assert.Equal(HttpStatusCode.OK, deliver.StatusCode);
        var delivered = await deliver.Content.ReadFromJsonAsync<ShopOrderDto>();
        Assert.Equal(ShopOrderStatusNames.Delivered, delivered!.Status);
    }

    [Fact]
    public async Task Queue_WithoutPermission_Returns403()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthorizeStaffForBranchAsync(factory, staffClient, seeded.OrganizationId, seeded.BranchId, withShopPermission: false);

        var response = await staffClient.GetAsync($"/api/branches/{seeded.BranchId:D}/shop/orders");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
