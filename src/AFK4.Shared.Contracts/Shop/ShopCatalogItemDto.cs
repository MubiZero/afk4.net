using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopCatalogItemDto(
    Guid ProductId,
    string Name,
    string Sku,
    MoneyDto Price,
    int StockOnHand);
