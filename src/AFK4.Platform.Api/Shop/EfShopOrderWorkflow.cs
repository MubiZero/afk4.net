using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Loyalty;
using AFK4.Platform.Api.Pos;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Inventory;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shop;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Shop;

public sealed class EfShopOrderWorkflow(
    PlatformDbContext dbContext,
    TimeProvider timeProvider,
    IShopOrderNotifier notifier,
    ILoyaltyAccrualService loyaltyAccrualService,
    ILogger<EfShopOrderWorkflow>? logger = null) : IShopOrderWorkflow
{
    public async Task<ShopPlacementContextResult> ResolvePlacementContextAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var player = await dbContext.PlayerAccounts.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlayerAccountId == playerAccountId, cancellationToken);
        if (player is null)
        {
            return new(false, "player_not_found", null);
        }

        var session = await dbContext.Sessions.AsNoTracking()
            .Where(candidate =>
                candidate.PlayerAccountId == playerAccountId &&
                (candidate.State == SessionStateNames.Active || candidate.State == SessionStateNames.Paused))
            .OrderByDescending(candidate => candidate.SessionId)
            .FirstOrDefaultAsync(cancellationToken);
        if (session is null)
        {
            return new(false, "no_active_session", null);
        }

        return new(true, null, new ShopPlacementContext(
            player.OrganizationId,
            session.BranchId,
            playerAccountId,
            session.SessionId,
            session.SeatId,
            player.DisplayName));
    }

    public Task<ShopOrderDto> CreatePlacedAsync(
        ShopPlacementContext context,
        ShopPosSettlementResult settlement,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(settlement.Sale);
        ArgumentNullException.ThrowIfNull(settlement.WalletEntry);

        var order = new ShopOrderEntity
        {
            ShopOrderId = Guid.NewGuid(),
            OrganizationId = context.OrganizationId,
            BranchId = context.BranchId,
            PlayerAccountId = context.PlayerAccountId,
            SessionId = context.SessionId,
            SeatId = context.SeatId,
            Status = ShopOrderStatusNames.Placed,
            TotalMinorUnits = settlement.Sale.TotalMinorUnits,
            CurrencyCode = settlement.Sale.CurrencyCode,
            WalletLedgerEntryId = settlement.WalletEntry.LedgerEntryId,
            PosSaleId = settlement.Sale.PosSaleId,
            PlacedAtUtc = now,
            Version = 1
        };
        var lines = settlement.Lines.Select(line => new ShopOrderLineEntity
        {
            ShopOrderLineId = Guid.NewGuid(),
            ShopOrderId = order.ShopOrderId,
            ProductId = line.ProductId,
            NameSnapshot = line.ProductName,
            UnitPriceMinorUnits = line.UnitPriceMinorUnits,
            Quantity = line.Quantity,
            LineTotalMinorUnits = line.LineTotalMinorUnits
        }).ToList();

        dbContext.ShopOrders.Add(order);
        dbContext.ShopOrderLines.AddRange(lines);
        return Task.FromResult(ShopOrderProjection.ToDto(order, lines, context.PlayerDisplayName));
    }

    public async Task<ShopCancellationContextResult> ResolveOperatorCancellationAsync(
        Guid branchId,
        Guid orderId,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders
            .SingleOrDefaultAsync(candidate => candidate.ShopOrderId == orderId && candidate.BranchId == branchId, cancellationToken);
        if (order is null)
        {
            return new(false, true, false, null, null, null);
        }

        if (order.Status == ShopOrderStatusNames.Cancelled)
        {
            return new(true, false, false, null, null, order);
        }

        if (expectedVersion is { } expected && expected != order.Version)
        {
            return new(false, false, true, "version_conflict", order.Version, order);
        }

        return order.Status == ShopOrderStatusNames.Delivered
            ? new(false, false, false, "invalid_transition", null, order)
            : new(true, false, false, null, null, order);
    }

    public async Task<ShopCancellationContextResult> ResolvePlayerCancellationAsync(
        Guid playerAccountId,
        Guid orderId,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders
            .SingleOrDefaultAsync(candidate =>
                candidate.ShopOrderId == orderId && candidate.PlayerAccountId == playerAccountId,
                cancellationToken);
        if (order is null)
        {
            return new(false, true, false, null, null, null);
        }

        return order.Status is ShopOrderStatusNames.Placed or ShopOrderStatusNames.Cancelled
            ? new(true, false, false, null, null, order)
            : new(false, false, false, "invalid_transition", null, order);
    }

    public async Task<ShopCancellationContextResult> ResolveLinkedSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders
            .SingleOrDefaultAsync(candidate => candidate.PosSaleId == saleId, cancellationToken);
        return order is null
            ? new(false, true, false, null, null, null)
            : new(true, false, false, null, null, order);
    }

    public Task<ShopOrderDto> MarkCancelledAsync(
        ShopOrderEntity order,
        Guid actorStaffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (order.Status != ShopOrderStatusNames.Cancelled)
        {
            order.Status = ShopOrderStatusNames.Cancelled;
            order.CancelledAtUtc = now;
            order.Version += 1;
        }

        return ProjectAsync(order, cancellationToken);
    }

    public async Task<ShopOrderActionResult> CancelLegacyAsync(
        ShopOrderEntity order,
        Guid actorStaffUserId,
        CancellationToken cancellationToken)
    {
        if (order.Status == ShopOrderStatusNames.Cancelled)
        {
            return ShopOrderActionResult.Ok(await ProjectAsync(order, cancellationToken));
        }

        var now = timeProvider.GetUtcNow();
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(line => line.ShopOrderId == order.ShopOrderId)
            .ToListAsync(cancellationToken);
        dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
            order.OrganizationId,
            order.BranchId,
            order.PlayerAccountId,
            order.SessionId,
            null,
            LedgerEntryTypeNames.Reversal,
            LedgerAccountTypeNames.Wallet,
            order.TotalMinorUnits,
            0,
            order.CurrencyCode,
            "shop_order_cancel",
            order.ShopOrderId.ToString("D"),
            order.WalletLedgerEntryId,
            actorStaffUserId,
            now));
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
                CreatedByStaffUserId = actorStaffUserId,
                CreatedAtUtc = now
            });
        }

        return ShopOrderActionResult.Ok(await MarkCancelledAsync(order, actorStaffUserId, now, cancellationToken));
    }

    public async Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken)
    {
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(order => order.PlayerAccountId == playerAccountId)
            .OrderByDescending(order => order.PlacedAtUtc)
            .Take(20)
            .ToListAsync(cancellationToken);
        return await ProjectManyAsync(orders, cancellationToken);
    }

    public async Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var open = new[] { ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted };
        var orders = await dbContext.ShopOrders.AsNoTracking()
            .Where(order => order.BranchId == branchId && open.Contains(order.Status))
            .OrderBy(order => order.PlacedAtUtc)
            .ToListAsync(cancellationToken);
        return await ProjectManyAsync(orders, cancellationToken);
    }

    public Task<ShopOrderActionResult> AcceptAsync(
        Guid branchId, Guid orderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, orderId, expectedVersion, ShopOrderStatusNames.Placed, ShopOrderStatusNames.Accepted, cancellationToken);

    public Task<ShopOrderActionResult> DeliverAsync(
        Guid branchId, Guid orderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        TransitionAsync(branchId, orderId, expectedVersion, ShopOrderStatusNames.Accepted, ShopOrderStatusNames.Delivered, cancellationToken);

    public async Task<ShopOrderDto> ProjectAsync(ShopOrderEntity order, CancellationToken cancellationToken)
    {
        var lines = await dbContext.ShopOrderLines.AsNoTracking()
            .Where(line => line.ShopOrderId == order.ShopOrderId)
            .ToListAsync(cancellationToken);
        var playerName = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(player => player.PlayerAccountId == order.PlayerAccountId)
            .Select(player => player.DisplayName)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;
        return ShopOrderProjection.ToDto(order, lines, playerName);
    }

    private async Task<ShopOrderActionResult> TransitionAsync(
        Guid branchId,
        Guid orderId,
        int? expectedVersion,
        string fromStatus,
        string toStatus,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.ShopOrders
            .SingleOrDefaultAsync(candidate => candidate.ShopOrderId == orderId && candidate.BranchId == branchId, cancellationToken);
        if (order is null) return ShopOrderActionResult.Missing();
        if (expectedVersion is { } expected && expected != order.Version) return ShopOrderActionResult.VersionConflict(order.Version);
        if (order.Status != fromStatus) return ShopOrderActionResult.Business("invalid_transition");

        var now = timeProvider.GetUtcNow();
        order.Status = toStatus;
        order.Version += 1;
        if (toStatus == ShopOrderStatusNames.Accepted)
        {
            order.AcceptedAtUtc = now;
        }
        else
        {
            order.DeliveredAtUtc = now;
            var cashback = await loyaltyAccrualService.BuildCashbackEntryAsync(
                LoyaltyAccrualSource.Shop,
                order.OrganizationId,
                order.BranchId,
                order.PlayerAccountId,
                order.SessionId,
                order.TotalMinorUnits,
                order.CurrencyCode,
                $"cashback:shop:{order.ShopOrderId:D}",
                now,
                cancellationToken);
            if (cashback is not null) dbContext.LedgerEntries.Add(cashback);
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            dbContext.ChangeTracker.Clear();
            var currentVersion = await dbContext.ShopOrders
                .AsNoTracking()
                .Where(candidate => candidate.ShopOrderId == orderId && candidate.BranchId == branchId)
                .Select(candidate => (int?)candidate.Version)
                .SingleOrDefaultAsync(cancellationToken);
            return ShopOrderActionResult.VersionConflict(currentVersion);
        }

        var dto = await ProjectAsync(order, cancellationToken);
        try
        {
            await notifier.NotifyUpdatedAsync(dto, cancellationToken);
        }
        catch (Exception exception)
        {
            logger?.LogWarning(exception, "Failed to publish updated shop order {ShopOrderId}.", order.ShopOrderId);
        }

        return ShopOrderActionResult.Ok(dto);
    }

    private async Task<IReadOnlyList<ShopOrderDto>> ProjectManyAsync(
        IReadOnlyList<ShopOrderEntity> orders,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0) return [];
        var orderIds = orders.Select(order => order.ShopOrderId).ToList();
        var playerIds = orders.Select(order => order.PlayerAccountId).Distinct().ToList();
        var linesByOrder = (await dbContext.ShopOrderLines.AsNoTracking()
                .Where(line => orderIds.Contains(line.ShopOrderId))
                .ToListAsync(cancellationToken))
            .GroupBy(line => line.ShopOrderId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<ShopOrderLineEntity>)group.ToList());
        var names = await dbContext.PlayerAccounts.AsNoTracking()
            .Where(player => playerIds.Contains(player.PlayerAccountId))
            .ToDictionaryAsync(player => player.PlayerAccountId, player => player.DisplayName, cancellationToken);
        return orders.Select(order => ShopOrderProjection.ToDto(
            order,
            linesByOrder.GetValueOrDefault(order.ShopOrderId, []),
            names.GetValueOrDefault(order.PlayerAccountId, string.Empty))).ToList();
    }
}
