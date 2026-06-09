using AFK4.Shared.Contracts.Shop;

namespace AFK4.Platform.Api.Shop;

public interface IShopOrderNotifier
{
    Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken);

    Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken);
}
