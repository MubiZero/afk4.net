using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Endpoints;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    [Fact]
    public async Task CancelOrder_ByOperator_RefundsLinkedSale()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        using var playerClient = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(playerClient, seeded);
        var placed = await (await playerClient.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 2)], "operator-cancel-001")))
            .Content.ReadFromJsonAsync<ShopOrderDto>();
        await ShopTestSeed.AuthorizeStaffForBranchAsync(
            factory, staffClient, seeded.OrganizationId, seeded.BranchId, withShopPermission: true);

        var request = new ShopOrderActionRequest(placed!.Version);
        var first = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed.Id:D}/cancel", request);
        var second = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed.Id:D}/cancel", request);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        await PlayerShopEndpointTests.AssertLinkedRefundStateAsync(factory, placed.Id, placed.PosSaleId!.Value);
    }

    [Fact]
    public async Task CancelLegacyOrder_UsesCompatibilityPath()
    {
        await using var factory = new PlatformApiFactory();
        using var staffClient = factory.CreateClient();
        using var playerClient = factory.CreateClient();
        var seeded = await ShopTestSeed.SeedActivePlayerWithProductsAsync(factory);
        await ShopTestSeed.AuthenticatePlayerAsync(playerClient, seeded);
        var placed = await (await playerClient.PostAsJsonAsync("/api/me/shop/orders",
            new PlaceShopOrderRequest([new ShopOrderLineInput(seeded.ColaProductId, 2)], "legacy-cancel-001")))
            .Content.ReadFromJsonAsync<ShopOrderDto>() ?? throw new InvalidOperationException("Placement response was empty.");

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var order = await db.ShopOrders.SingleAsync(candidate => candidate.ShopOrderId == placed.Id);
            order.PosSaleId = null;
            db.Payments.RemoveRange(db.Payments.Where(payment => payment.PosSaleId == placed.PosSaleId));
            db.Receipts.RemoveRange(db.Receipts.Where(receipt => receipt.PosSaleId == placed.PosSaleId));
            db.StockMovements.RemoveRange(db.StockMovements.Where(movement => movement.MovementType == StockMovementTypeNames.Sale));
            db.PosSales.Remove(await db.PosSales.SingleAsync(sale => sale.PosSaleId == placed.PosSaleId));
            await db.SaveChangesAsync();
        }
        await ShopTestSeed.AuthorizeStaffForBranchAsync(
            factory, staffClient, seeded.OrganizationId, seeded.BranchId, withShopPermission: true);

        var response = await staffClient.PostAsJsonAsync(
            $"/api/branches/{seeded.BranchId:D}/shop/orders/{placed.Id:D}/cancel",
            new ShopOrderActionRequest(placed.Version));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var orderAfter = await verificationDb.ShopOrders.SingleAsync(order => order.ShopOrderId == placed.Id);
        Assert.Equal(ShopOrderStatusNames.Cancelled, orderAfter.Status);
        Assert.Empty(await verificationDb.PosSales.ToListAsync());
        Assert.Empty(await verificationDb.Payments.ToListAsync());
        Assert.Empty(await verificationDb.Receipts.ToListAsync());
        Assert.Equal(2, await verificationDb.LedgerEntries.CountAsync(entry =>
            entry.LedgerEntryId == orderAfter.WalletLedgerEntryId ||
            entry.ReversesLedgerEntryId == orderAfter.WalletLedgerEntryId));
        Assert.Equal(1, await verificationDb.StockMovements.CountAsync(movement =>
            movement.MovementType == StockMovementTypeNames.Refund));
    }
}
