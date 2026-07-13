using System.Text.Json;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ShopContractSerializationTests
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [Fact]
    public void PlaceShopOrderRequest_RoundTripsIdempotencyKey()
    {
        var request = new PlaceShopOrderRequest(
            [new ShopOrderLineInput(Guid.Parse("dddddddd-0000-0000-0000-000000000001"), 2)],
            "shop-place-001");

        var json = JsonSerializer.Serialize(request, Options);
        var copy = JsonSerializer.Deserialize<PlaceShopOrderRequest>(json, Options);

        Assert.Equal("shop-place-001", copy!.IdempotencyKey);
        Assert.Contains("\"idempotencyKey\"", json);
    }

    [Fact]
    public void ShopAndPosDtos_RoundTripLinkedIds()
    {
        var saleId = Guid.Parse("eeeeeeee-0000-0000-0000-000000000001");
        var orderId = Guid.Parse("ffffffff-0000-0000-0000-000000000001");
        var branchId = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
        var playerId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
        var order = new ShopOrderDto(orderId, branchId, Guid.NewGuid(), playerId, "Player",
            ShopOrderStatusNames.Placed, new MoneyDto("TJS", 500), [],
            DateTimeOffset.Parse("2026-07-13T10:00:00Z"), null, null, null, 1, saleId);
        var sale = new PosSaleDto(saleId, Guid.NewGuid(), branchId, Guid.NewGuid(),
            PosSaleStateNames.Paid, [], new MoneyDto("TJS", 500), Guid.Empty,
            DateTimeOffset.Parse("2026-07-13T10:00:00Z"),
            DateTimeOffset.Parse("2026-07-13T10:00:00Z"), null, null,
            LatestReceipt: null, PlayerAccountId: playerId, ShopOrderId: orderId);

        var orderCopy = JsonSerializer.Deserialize<ShopOrderDto>(JsonSerializer.Serialize(order, Options), Options);
        var saleCopy = JsonSerializer.Deserialize<PosSaleDto>(JsonSerializer.Serialize(sale, Options), Options);

        Assert.Equal(saleId, orderCopy!.PosSaleId);
        Assert.Equal(orderId, saleCopy!.ShopOrderId);
    }
}
