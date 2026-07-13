using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Pos;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Pos;
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
        Assert.Equal(order.BranchId, dto.BranchId);
        Assert.Equal(order.SeatId, dto.SeatId);
        Assert.Equal(order.PlayerAccountId, dto.PlayerAccountId);
        Assert.Equal(ShopOrderStatusNames.Placed, dto.Status);
        Assert.Equal(DateTimeOffset.UnixEpoch, dto.PlacedAtUtc);
        Assert.Equal(1, dto.Version);
        Assert.Null(dto.AcceptedAtUtc);
        Assert.Null(dto.DeliveredAtUtc);
        Assert.Null(dto.CancelledAtUtc);
        Assert.Equal("TJS", line.UnitPrice.CurrencyCode);
        Assert.Equal("TJS", line.LineTotal.CurrencyCode);
    }

    [Fact]
    public void LinkedOrderSaleAndReceipt_KeepRetailTotalsAndCostSnapshotAligned()
    {
        var saleId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = new ShopOrderEntity
        {
            ShopOrderId = orderId,
            BranchId = Guid.NewGuid(),
            SeatId = Guid.NewGuid(),
            PlayerAccountId = Guid.NewGuid(),
            PosSaleId = saleId,
            Status = ShopOrderStatusNames.Placed,
            TotalMinorUnits = 1500,
            CurrencyCode = "TJS",
            PlacedAtUtc = DateTimeOffset.UnixEpoch,
            Version = 1
        };
        var orderLine = new ShopOrderLineEntity
        {
            ShopOrderLineId = Guid.NewGuid(),
            ShopOrderId = orderId,
            ProductId = productId,
            NameSnapshot = "Cola",
            UnitPriceMinorUnits = 500,
            Quantity = 3,
            LineTotalMinorUnits = 1500
        };
        var sale = new PosSaleEntity
        {
            PosSaleId = saleId,
            OrganizationId = Guid.NewGuid(),
            BranchId = order.BranchId,
            ShiftId = Guid.NewGuid(),
            State = PosSaleStateNames.Paid,
            CurrencyCode = "TJS",
            TotalMinorUnits = 1500,
            CreatedAtUtc = DateTimeOffset.UnixEpoch,
            PaidAtUtc = DateTimeOffset.UnixEpoch
        };
        var saleLine = new PosSaleLineEntity
        {
            PosSaleLineId = Guid.NewGuid(),
            PosSaleId = saleId,
            ProductId = productId,
            ProductName = "Cola",
            Quantity = 3,
            CurrencyCode = "TJS",
            UnitPriceMinorUnits = 500,
            UnitCostMinorUnits = 275,
            LineTotalMinorUnits = 1500,
            TracksStock = true
        };
        var receipt = new ReceiptEntity
        {
            ReceiptId = Guid.NewGuid(),
            OrganizationId = sale.OrganizationId,
            BranchId = sale.BranchId,
            PosSaleId = saleId,
            ReceiptNumber = "R-1",
            ReceiptType = "sale",
            CurrencyCode = "TJS",
            TotalMinorUnits = 1500,
            CreatedAtUtc = DateTimeOffset.UnixEpoch
        };

        var orderDto = ShopOrderProjection.ToDto(order, [orderLine], "Alex");
        var saleDto = PosSaleProjection.ToDto(sale, [saleLine], receipt, orderId);

        Assert.Equal(saleId, orderDto.PosSaleId);
        Assert.Equal(orderId, saleDto.ShopOrderId);
        Assert.Equal(orderId, saleDto.LatestReceipt!.ShopOrderId);
        Assert.Equal(orderDto.Total, saleDto.Total);
        Assert.Equal(orderDto.Total, saleDto.LatestReceipt.Total);
        Assert.Equal(Assert.Single(orderDto.Lines).LineTotal, Assert.Single(saleDto.Lines).LineTotal);
        Assert.Equal(275, saleLine.UnitCostMinorUnits);
        Assert.NotEqual(saleLine.UnitPriceMinorUnits, saleLine.UnitCostMinorUnits);
    }
}
