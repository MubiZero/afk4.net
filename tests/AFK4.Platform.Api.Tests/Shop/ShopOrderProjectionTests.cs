using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Shop;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class ShopOrderProjectionTests
{
    [Fact]
    public void ToDto_MapsOrderAndLines()
    {
        var order = new ShopOrderEntity
        {
            ShopOrderId = Guid.NewGuid(),
            BranchId = Guid.NewGuid(),
            SeatId = Guid.NewGuid(),
            PlayerAccountId = Guid.NewGuid(),
            Status = ShopOrderStatusNames.Placed,
            TotalMinorUnits = 1500,
            CurrencyCode = "TJS",
            PlacedAtUtc = DateTimeOffset.UnixEpoch,
            Version = 1
        };
        var lines = new[]
        {
            new ShopOrderLineEntity
            {
                ShopOrderLineId = Guid.NewGuid(),
                ShopOrderId = order.ShopOrderId,
                ProductId = Guid.NewGuid(),
                NameSnapshot = "Cola",
                UnitPriceMinorUnits = 500,
                Quantity = 3,
                LineTotalMinorUnits = 1500
            }
        };

        var dto = ShopOrderProjection.ToDto(order, lines, playerDisplayName: "Alex");

        Assert.Equal(order.ShopOrderId, dto.Id);
        Assert.Equal("Alex", dto.PlayerDisplayName);
        Assert.Equal(1500, dto.Total.MinorUnits);
        Assert.Equal("TJS", dto.Total.CurrencyCode);
        var line = Assert.Single(dto.Lines);
        Assert.Equal("Cola", line.Name);
        Assert.Equal(3, line.Quantity);
        Assert.Equal(500, line.UnitPrice.MinorUnits);
        Assert.Equal(1500, line.LineTotal.MinorUnits);
    }
}
