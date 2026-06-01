using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Inventory;

public sealed class EfLowStockNotifier(
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications,
    PlatformDbContext dbContext) : ILowStockNotifier
{
    public async Task EvaluateProductsAsync(
        Guid organizationId,
        Guid branchId,
        IReadOnlyCollection<Guid> productIds,
        CancellationToken cancellationToken)
    {
        foreach (var productId in productIds.Distinct())
        {
            await EvaluateOneAsync(organizationId, branchId, productId, cancellationToken);
        }
    }

    private async Task EvaluateOneAsync(Guid organizationId, Guid branchId, Guid productId, CancellationToken cancellationToken)
    {
        var product = await dbContext.PosProducts.FirstOrDefaultAsync(
            candidate => candidate.OrganizationId == organizationId
                && candidate.BranchId == branchId
                && candidate.ProductId == productId,
            cancellationToken);
        if (product is null || !product.TrackStock || product.ReorderThreshold <= 0)
        {
            return;
        }

        var stockOnHand = await dbContext.StockMovements
            .Where(movement =>
                movement.OrganizationId == organizationId &&
                movement.BranchId == branchId &&
                movement.ProductId == productId)
            .SumAsync(movement => (int?)movement.QuantityDelta, cancellationToken) ?? 0;

        if (stockOnHand > product.ReorderThreshold)
        {
            if (product.LowStockAlerted)
            {
                // Stock recovered — re-arm so the next dip alerts again under a fresh cycle key.
                product.LowStockAlerted = false;
                product.LowStockCycle++;
            }
            return;
        }

        if (product.LowStockAlerted)
        {
            return;
        }

        product.LowStockAlerted = true;

        var recipient = await ownerResolver.ResolveAsync(organizationId, cancellationToken);
        if (recipient is null)
        {
            return;
        }

        var branchName = await dbContext.Branches
            .Where(branch => branch.BranchId == branchId)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.LowStock,
            Category: NotificationCategory.Operational,
            Recipient: new NotificationRecipient(
                Locale: string.Empty,
                EmailAddress: recipient.Email,
                StaffUserId: recipient.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = recipient.DisplayName,
                ["organizationName"] = recipient.OrganizationName,
                ["branchName"] = branchName,
                ["productName"] = product.Name,
                ["sku"] = product.Sku,
                ["stockOnHand"] = stockOnHand.ToString(CultureInfo.InvariantCulture),
                ["threshold"] = product.ReorderThreshold.ToString(CultureInfo.InvariantCulture),
            },
            IdempotencyKey: $"inventory-low-stock:{productId:N}:{product.LowStockCycle}",
            OrganizationId: organizationId,
            BranchId: branchId);

        await notifications.SendAsync(request, cancellationToken);
    }
}
