namespace AFK4.Platform.Api.Inventory;

/// <summary>
/// After stock movements are persisted, alerts the organization owner once per restock cycle when a
/// tracked product's stock-on-hand reaches its reorder threshold (Operational). Re-arms when stock
/// returns above the threshold. A no-op for untracked products, a zero threshold, or no owner email.
/// </summary>
public interface ILowStockNotifier
{
    Task EvaluateProductsAsync(
        Guid organizationId,
        Guid branchId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken);
}
