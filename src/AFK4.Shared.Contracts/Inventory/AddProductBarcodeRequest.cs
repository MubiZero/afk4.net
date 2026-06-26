namespace AFK4.Shared.Contracts.Inventory;

public sealed record AddProductBarcodeRequest(Guid OrganizationId, string Code, bool IsPrimary = false);
