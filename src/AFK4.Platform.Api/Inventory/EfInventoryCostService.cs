using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Inventory;

public sealed class EfInventoryCostService(PlatformDbContext dbContext) : IInventoryCostService
{
    public static long MovingWeightedAverage(
        long currentQuantity,
        long currentUnitCost,
        int inboundQuantity,
        long inboundUnitCost)
    {
        var baseQuantity = Math.Max(currentQuantity, 0);
        var denominator = checked(baseQuantity + inboundQuantity);
        return denominator <= 0
            ? inboundUnitCost
            : (long)Math.Round(
                (baseQuantity * (double)currentUnitCost + inboundQuantity * (double)inboundUnitCost) / denominator,
                MidpointRounding.AwayFromZero);
    }

    public async Task ReconcileInboundAsync(
        Guid organizationId,
        Guid branchId,
        Guid productId,
        int quantity,
        string currencyCode,
        long unitCostMinorUnits,
        CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var product = await dbContext.PosProducts.SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.BranchId == branchId &&
                candidate.ProductId == productId,
            cancellationToken);

        if (product is null)
        {
            throw new InvalidOperationException("Product was not found.");
        }

        if (!string.Equals(
                product.CurrencyCode.Trim(),
                currencyCode.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("inventory_currency_mismatch");
        }

        var currentQuantity = await dbContext.StockMovements
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId &&
                movement.ProductId == productId)
            .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;

        product.AvgCostMinorUnits = MovingWeightedAverage(
            currentQuantity,
            product.AvgCostMinorUnits,
            quantity,
            unitCostMinorUnits);
    }
}
