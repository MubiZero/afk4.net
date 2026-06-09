namespace AFK4.Shared.Contracts.Shop;

public sealed record ShopOrderLineInput(Guid ProductId, int Quantity);
