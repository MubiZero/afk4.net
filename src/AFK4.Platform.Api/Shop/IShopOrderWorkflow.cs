using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Pos;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public sealed record ShopPlacementContext(
    Guid OrganizationId,
    Guid BranchId,
    Guid PlayerAccountId,
    Guid SessionId,
    Guid SeatId,
    string PlayerDisplayName);

public sealed record ShopPlacementContextResult(
    bool Succeeded,
    string? ErrorCode,
    ShopPlacementContext? Context);

public sealed record ShopCancellationContextResult(
    bool Succeeded,
    bool NotFound,
    bool Conflict,
    string? ErrorCode,
    int? CurrentVersion,
    ShopOrderEntity? Order);

public interface IShopOrderWorkflow
{
    Task<ShopPlacementContextResult> ResolvePlacementContextAsync(
        Guid playerAccountId, CancellationToken cancellationToken);

    Task<ShopOrderDto> CreatePlacedAsync(
        ShopPlacementContext context,
        ShopPosSettlementResult settlement,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ShopCancellationContextResult> ResolveOperatorCancellationAsync(
        Guid branchId,
        Guid orderId,
        int? expectedVersion,
        CancellationToken cancellationToken);

    Task<ShopCancellationContextResult> ResolvePlayerCancellationAsync(
        Guid playerAccountId,
        Guid orderId,
        CancellationToken cancellationToken);

    Task<ShopCancellationContextResult> ResolveLinkedSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken);

    Task<ShopOrderDto> MarkCancelledAsync(
        ShopOrderEntity order,
        Guid actorStaffUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelLegacyAsync(
        ShopOrderEntity order,
        Guid actorStaffUserId,
        CancellationToken cancellationToken);

    Task<ShopOrderDto> ProjectAsync(ShopOrderEntity order, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> AcceptAsync(
        Guid branchId, Guid orderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> DeliverAsync(
        Guid branchId, Guid orderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);
}
