using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderLineDto(
    Guid ProductId,
    string Name,
    MoneyDto UnitPrice,
    int Quantity,
    MoneyDto LineTotal);
