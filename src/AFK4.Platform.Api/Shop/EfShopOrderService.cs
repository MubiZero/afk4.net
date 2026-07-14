using AFK4.Platform.Api.Commerce;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public sealed class EfShopOrderService(
    IShopCommerceCoordinator commerceCoordinator,
    IShopOrderWorkflow workflow) : IShopOrderService
{
    public Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, PlaceShopOrderRequest request, CancellationToken cancellationToken) =>
        commerceCoordinator.PlaceAsync(playerAccountId, request, cancellationToken);

    public Task<IReadOnlyList<ShopOrderDto>> ListForPlayerAsync(Guid playerAccountId, CancellationToken cancellationToken) =>
        workflow.ListForPlayerAsync(playerAccountId, cancellationToken);

    public Task<IReadOnlyList<ShopOrderDto>> ListQueueAsync(Guid branchId, CancellationToken cancellationToken) =>
        workflow.ListQueueAsync(branchId, cancellationToken);

    public Task<ShopOrderActionResult> AcceptAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        workflow.AcceptAsync(branchId, shopOrderId, staffUserId, expectedVersion, cancellationToken);

    public Task<ShopOrderActionResult> DeliverAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        workflow.DeliverAsync(branchId, shopOrderId, staffUserId, expectedVersion, cancellationToken);

    public Task<ShopOrderActionResult> CancelByOperatorAsync(
        Guid branchId, Guid shopOrderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken) =>
        commerceCoordinator.CancelByOperatorAsync(branchId, shopOrderId, staffUserId, expectedVersion, cancellationToken);

    public Task<ShopOrderActionResult> CancelByPlayerAsync(
        Guid playerAccountId, Guid shopOrderId, CancellationToken cancellationToken) =>
        commerceCoordinator.CancelByPlayerAsync(playerAccountId, shopOrderId, cancellationToken);
}
