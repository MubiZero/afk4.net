using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

/// <summary>Строка заказа: что и сколько.</summary>
public sealed record ShopOrderLineDto(
    Guid ProductId,
    string Name,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal);
