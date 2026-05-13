namespace AFK4.Shared.Contracts.Inventory;

public sealed record InventoryStockDto(
    Guid ProductId,
    string ProductName,
    string Sku,
    bool TrackStock,
    int StockOnHand);
