using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public static class ShopOrderProjection
{
    public static ShopOrderDto ToDto(
        ShopOrderEntity order,
        IReadOnlyCollection<ShopOrderLineEntity> lines,
        string playerDisplayName)
    {
        var lineDtos = lines
            .Select(line => new ShopOrderLineDto(
                line.ProductId,
                line.NameSnapshot,
                new MoneyDto(order.CurrencyCode, line.UnitPriceMinorUnits),
                line.Quantity,
                new MoneyDto(order.CurrencyCode, line.LineTotalMinorUnits)))
            .ToList();

        return new ShopOrderDto(
            order.ShopOrderId,
            order.BranchId,
            order.SeatId,
            order.PlayerAccountId,
            playerDisplayName,
            order.Status,
            new MoneyDto(order.CurrencyCode, order.TotalMinorUnits),
            lineDtos,
            order.PlacedAtUtc,
            order.AcceptedAtUtc,
            order.DeliveredAtUtc,
            order.CancelledAtUtc,
            order.Version,
            order.PosSaleId);
    }
}
