namespace AFK4.Shared.Contracts.Inventory;

public sealed record ProductBarcodeDto(Guid BarcodeId, Guid ProductId, string Code, bool IsPrimary);
