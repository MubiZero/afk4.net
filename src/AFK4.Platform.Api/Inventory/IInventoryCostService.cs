namespace AFK4.Platform.Api.Inventory;

public interface IInventoryCostService
{
    Task ReconcileInboundAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        int quantity,
        long unitCostMinorUnits,
        CancellationToken cancellationToken);
}
