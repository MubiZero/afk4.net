using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public interface IShopOrderService
{
    Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, PlaceShopOrderRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> AcceptAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> DeliverAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByOperatorAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByPlayerAsync(
        Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken);
}
