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

public static class LowStockNotifierExtensions
{
    /// <summary>
    /// The one place every stock-decrementing sale (POS checkout, bar order from a wallet, session
    /// checkout) reaches for its post-commit low-stock check, instead of each caller re-writing the
    /// same try/catch. The settlement is already durable by the time this runs, so a notification
    /// failure must never turn a committed financial command into an ambiguous client failure — any
    /// exception is logged and swallowed here rather than left to each call site.
    /// </summary>
    public static async Task NotifyAfterSaleAsync(
        this ILowStockNotifier? notifier,
        Guid organizationId,
        Guid branchId,
        IReadOnlyCollection<Guid> productIds,
        ILogger? logger,
        string logContext,
        CancellationToken cancellationToken)
    {
        if (notifier is null || productIds.Count == 0)
        {
            return;
        }

        try
        {
            await notifier.EvaluateProductsAsync(organizationId, branchId, productIds, cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Low-stock evaluation failed after {Context} committed.", logContext);
        }
    }
}
