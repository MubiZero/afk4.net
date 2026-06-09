using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shop;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Shop;

public sealed class SignalRShopOrderNotifier(IHubContext<DeviceHub> hubContext) : IShopOrderNotifier
{
    public Task NotifyCreatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DeviceHubGroups.Branch(order.BranchId))
            .SendAsync(DeviceRealtimeEvents.ShopOrderCreated, order, cancellationToken);

    public Task NotifyUpdatedAsync(ShopOrderDto order, CancellationToken cancellationToken) =>
        hubContext.Clients
            .Group(DeviceHubGroups.Branch(order.BranchId))
            .SendAsync(DeviceRealtimeEvents.ShopOrderUpdated, order, cancellationToken);
}
