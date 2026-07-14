namespace AFK4.Platform.Api.Inventory;

public interface IInventoryCostService
{
    Task ReconcileInboundAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        int quantity,
        string currencyCode,
        long unitCostMinorUnits,
        CancellationToken cancellationToken);
}
