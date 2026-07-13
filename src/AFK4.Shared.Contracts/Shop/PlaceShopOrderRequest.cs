namespace AFK4.Shared.Contracts.Shop;

public sealed record PlaceShopOrderRequest(
    IReadOnlyList<ShopOrderLineInput> Lines,
    string IdempotencyKey);
