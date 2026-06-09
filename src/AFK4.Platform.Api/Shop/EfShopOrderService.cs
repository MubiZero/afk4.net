using System.Data;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Shop;

public sealed class EfShopOrderService(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IShopOrderNotifier notifier) : IShopOrderService
{
    public async Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, IReadOnlyList<ShopOrderLineInput> lines, CancellationToken cancellationToken)
    {
        if (lines.Count == 0 || lines.Any(line => line.Quantity <= 0))
        {
            return ShopOrderActionResult.Business("empty_order");
        }

        var player = await dbContext.PlayerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(p => p.PlayerAccountId == playerAccountId, cancellationToken);
        if (player is null)
        {
            return ShopOrderActionResult.Missing();
        }

        var session = await dbContext.Sessions.AsNoTracking()
            .Where(s => s.PlayerAccountId == playerAccountId && (s.State == SessionStateNames.Active || s.State == SessionStateNames.Paused))
            .OrderByDescending(s => s.SessionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return ShopOrderActionResult.Business("no_active_session");
        }

        var requested = lines
            .GroupBy(line => line.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.Quantity));

        var products = await dbContext.PosProducts.AsNoTracking()
            .Where(p => p.BranchId == session.BranchId && requested.Keys.Contains(p.ProductId))
            .ToListAsync(cancellationToken);

        if (products.Count != requested.Count ||
            products.Any(p => !p.IsActive || !p.AvailableInShell))
        {
            return ShopOrderActionResult.Business("product_unavailable");
        }

        var currency = products[0].CurrencyCode;
        if (products.Any(p => p.CurrencyCode != currency))
        {
            return ShopOrderActionResult.Business("mixed_currency");
        }

        var productIds = requested.Keys.ToList();

        return await ExecuteInTransactionAsync(async () =>
        {
            var onHand = await dbContext.StockMovements.AsNoTracking()
                .Where(m => m.BranchId == session.BranchId && productIds.Contains(m.ProductId))
                .GroupBy(m => m.ProductId)
                .Select(g => new { ProductId = g.Key, Quantity = g.Sum(m => m.QuantityDelta) })
                .ToDictionaryAsync(x => x.ProductId, x => x.Quantity, cancellationToken);

            foreach (var product in products.Where(p => p.TrackStock && !p.AllowNegativeStock))
            {
                if (onHand.GetValueOrDefault(product.ProductId) < requested[product.ProductId])
                {
                    return ShopOrderActionResult.Business("out_of_stock");
                }
            }

            var total = products.Sum(p => (long)requested[p.ProductId] * p.PriceMinorUnits);

            var wallet = await dbContext.LedgerEntries.AsNoTracking()
                .Where(e => e.PlayerAccountId == playerAccountId && e.AccountType == LedgerAccountTypeNames.Wallet)
                .SumAsync(e => (long?)e.AmountMinorUnits, cancellationToken) ?? 0;
            if (wallet < total)
            {
                return ShopOrderActionResult.Business("insufficient_funds");
            }

            var now = timeProvider.GetUtcNow();
            var orderId = Guid.NewGuid();

            var debit = BillingEntryFactory.Create(
                player.OrganizationId, session.BranchId, playerAccountId, session.SessionId, null,
                LedgerEntryTypeNames.WalletPayment, LedgerAccountTypeNames.Wallet,
                -total, 0, currency, "shop_order", orderId.ToString("D"),
                reversesLedgerEntryId: null, actorStaffUserId: Guid.Empty, createdAtUtc: now);
            dbContext.LedgerEntries.Add(debit);

            foreach (var product in products)
            {
                dbContext.StockMovements.Add(new StockMovementEntity
                {
                    StockMovementId = Guid.NewGuid(),
                    OrganizationId = player.OrganizationId,
                    BranchId = session.BranchId,
                    ProductId = product.ProductId,
                    MovementType = StockMovementTypeNames.Sale,
                    QuantityDelta = -requested[product.ProductId],
                    CurrencyCode = currency,
                    UnitCostMinorUnits = 0,
                    Reason = "shop_order",
                    CreatedByStaffUserId = Guid.Empty,
                    CreatedAtUtc = now
                });
            }

            var order = new ShopOrderEntity
            {
                ShopOrderId = orderId,
                OrganizationId = player.OrganizationId,
                BranchId = session.BranchId,
                PlayerAccountId = playerAccountId,
                SessionId = session.SessionId,
                SeatId = session.SeatId,
                Status = ShopOrderStatusNames.Placed,
                TotalMinorUnits = total,
                CurrencyCode = currency,
                WalletLedgerEntryId = debit.LedgerEntryId,
                PlacedAtUtc = now,
                Version = 1
            };
            dbContext.ShopOrders.Add(order);

            var lineEntities = products.Select(product => new ShopOrderLineEntity
            {
                ShopOrderLineId = Guid.NewGuid(),
                ShopOrderId = orderId,
                ProductId = product.ProductId,
                NameSnapshot = product.Name,
                UnitPriceMinorUnits = product.PriceMinorUnits,
                Quantity = requested[product.ProductId],
                LineTotalMinorUnits = (long)requested[product.ProductId] * product.PriceMinorUnits
            }).ToList();
            dbContext.ShopOrderLines.AddRange(lineEntities);

            await dbContext.SaveChangesAsync(cancellationToken);

            var dto = ShopOrderProjection.ToDto(order, lineEntities, player.DisplayName);
            await notifier.NotifyCreatedAsync(dto, cancellationToken);
            return ShopOrderActionResult.Ok(dto);
        }, cancellationToken);
    }

    // Mirrors the wallet/stock mutation guard used by EfBillingCommandService/EfInventoryService:
    // serializable transaction on relational providers, plain execution on the in-memory test provider
    // (BeginTransaction throws there).
    private async Task<ShopOrderActionResult> ExecuteInTransactionAsync(
        Func<Task<ShopOrderActionResult>> action, CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await action();
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var result = await action();
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(o => o.PlayerAccountId == playerAccountId)
            .OrderByDescending(o => o.PlacedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return await ProjectAsync(orders, cancellationToken);
    }

    public async Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var open = new[] { ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted };
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(o => o.BranchId == branchId && open.Contains(o.Status))
            .OrderBy(o => o.PlacedAtUtc)
            .ToListAsync(cancellationToken);
        return await ProjectAsync(orders, cancellationToken);
    }

    public Task<ShopOrderActionResult> AcceptAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, shopOrderId, expectedVersion, ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted, cancellationToken);

    public Task<ShopOrderActionResult> DeliverAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, shopOrderId, expectedVersion, ShopOrderStatusNames.Accepted, ShopOrderStatusNames.Delivered, cancellationToken);

    public async Task<ShopOrderActionResult> CancelByOperatorAsync(Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.BranchId == branchId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (expectedVersion is { } expected && expected != order.Version) return ShopOrderActionResult.VersionConflict(order.Version);
        if (order.Status is ShopOrderStatusNames.Delivered or ShopOrderStatusNames.Cancelled) return ShopOrderActionResult.Business("invalid_transition");
        return await ExecuteInTransactionAsync(() => CancelInternalAsync(order, staffUserId, cancellationToken), cancellationToken);
    }

    public async Task<ShopOrderActionResult> CancelByPlayerAsync(Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.PlayerAccountId == playerAccountId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (order.Status != ShopOrderStatusNames.Placed) return ShopOrderActionResult.Business("invalid_transition");
        return await ExecuteInTransactionAsync(() => CancelInternalAsync(order, Guid.Empty, cancellationToken), cancellationToken);
    }

    private async Task<ShopOrderActionResult> TransitionAsync(
        Guid branchId, Guid shopOrderId, int? expectedVersion, string fromStatus, string toStatus, CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders.SingleOrDefaultAsync(o => o.ShopOrderId == shopOrderId && o.BranchId == branchId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (expectedVersion is { } expected && expected != order.Version) return ShopOrderActionResult.VersionConflict(order.Version);
        if (order.Status != fromStatus) return ShopOrderActionResult.Business("invalid_transition");

        var now = timeProvider.GetUtcNow();
        order.Status = toStatus;
        order.Version += 1;
        if (toStatus == ShopOrderStatusNames.Accepted) order.AcceptedAtUtc = now;
        else if (toStatus == ShopOrderStatusNames.Delivered) order.DeliveredAtUtc = now;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ShopOrderActionResult.VersionConflict(null);
        }

        var dto = await ProjectSingleAsync(order, cancellationToken);
        await notifier.NotifyUpdatedAsync(dto, cancellationToken);
        return ShopOrderActionResult.Ok(dto);
    }

    private async Task<ShopOrderActionResult> CancelInternalAsync(ShopOrderEntity order, Guid staffUserId, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(l => l.ShopOrderId == order.ShopOrderId).ToListAsync(cancellationToken);

        dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
            order.OrganizationId, order.BranchId, order.PlayerAccountId, order.SessionId, null,
            LedgerEntryTypeNames.Reversal, LedgerAccountTypeNames.Wallet,
            order.TotalMinorUnits, 0, order.CurrencyCode, "shop_order_cancel", order.ShopOrderId.ToString("D"),
            reversesLedgerEntryId: order.WalletLedgerEntryId, actorStaffUserId: staffUserId, createdAtUtc: now));

        foreach (var line in lines)
        {
            dbContext.StockMovements.Add(new StockMovementEntity
            {
                StockMovementId = Guid.NewGuid(),
                OrganizationId = order.OrganizationId,
                BranchId = order.BranchId,
                ProductId = line.ProductId,
                MovementType = StockMovementTypeNames.Refund,
                QuantityDelta = line.Quantity,
                CurrencyCode = order.CurrencyCode,
                UnitCostMinorUnits = 0,
                Reason = "shop_order_cancel",
                CreatedByStaffUserId = staffUserId,
                CreatedAtUtc = now
            });
        }

        order.Status = ShopOrderStatusNames.Cancelled;
        order.CancelledAtUtc = now;
        order.Version += 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ShopOrderActionResult.VersionConflict(null);
        }

        var dto = await ProjectSingleAsync(order, cancellationToken);
        await notifier.NotifyUpdatedAsync(dto, cancellationToken);
        return ShopOrderActionResult.Ok(dto);
    }

    private async Task<ShopOrderDto> ProjectSingleAsync(ShopOrderEntity order, CancellationToken cancellationToken)
    {
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(l => l.ShopOrderId == order.ShopOrderId).ToListAsync(cancellationToken);
        var name = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(p => p.PlayerAccountId == order.PlayerAccountId).Select(p => p.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return ShopOrderProjection.ToDto(order, lines, name);
    }

    private async Task<IReadOnlyList<ShopOrderDto>> ProjectAsync(
        IReadOnlyList<ShopOrderEntity> orders, CancellationToken cancellationToken)
    {
        if (orders.Count == 0) return Array.Empty<ShopOrderDto>();
        var orderIds = orders.Select(o => o.ShopOrderId).ToList();
        var playerIds = orders.Select(o => o.PlayerAccountId).Distinct().ToList();
        var linesByOrder = (await dbContext.ShopOrderLines.AsNoTracking()
                .Where(l => orderIds.Contains(l.ShopOrderId)).ToListAsync(cancellationToken))
            .GroupBy(l => l.ShopOrderId).ToDictionary(g => g.Key, g => (IReadOnlyCollection<ShopOrderLineEntity>)g.ToList());
        var names = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(p => playerIds.Contains(p.PlayerAccountId))
            .ToDictionaryAsync(p => p.PlayerAccountId, p => p.DisplayName, cancellationToken);

        return orders.Select(o => ShopOrderProjection.ToDto(
            o,
            linesByOrder.GetValueOrDefault(o.ShopOrderId, Array.Empty<ShopOrderLineEntity>()),
            names.GetValueOrDefault(o.PlayerAccountId, string.Empty))).ToList();
    }
}
