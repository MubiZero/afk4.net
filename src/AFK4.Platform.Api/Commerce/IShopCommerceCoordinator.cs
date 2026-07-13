using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Commerce;

public interface IShopCommerceCoordinator
{
    Task<ShopOrderActionResult> PlaceAsync(
        Guid playerAccountId, PlaceShopOrderRequest request, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByOperatorAsync(
        Guid branchId, Guid orderId, Guid staffUserId, int? expectedVersion, CancellationToken cancellationToken);

    Task<ShopOrderActionResult> CancelByPlayerAsync(
        Guid playerAccountId, Guid orderId, CancellationToken cancellationToken);

    Task<BillingCommandServiceResult<PosSaleDto>> RefundLinkedSaleAsync(
        Guid saleId, Guid staffUserId, RefundPosSaleRequest request, CancellationToken cancellationToken);
}
