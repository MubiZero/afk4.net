using AFK4.Platform.Api.Devices;
using AFK4.Platform.Api.Shop;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shop;
using Microsoft.AspNetCore.SignalR;
using Xunit;

namespace AFK4.Platform.Api.Tests.Shop;

public sealed class SignalRShopOrderNotifierTests
{
    [Fact]
    public async Task NotifyCreated_SendsToBranchGroup()
    {
        var branchId = Guid.NewGuid();
        var order = new ShopOrderDto(
            Guid.NewGuid(), branchId, Guid.NewGuid(), Guid.NewGuid(), "Alex",
            ShopOrderStatusNames.Placed, new MoneyDto("TJS", 1500),
            Array.Empty<ShopOrderLineDto>(), DateTimeOffset.UnixEpoch, null, null, null, 1);

        var clients = new CapturingHubClients();
        var hubContext = new CapturingHubContext(clients);

        var notifier = new SignalRShopOrderNotifier(hubContext);
        await notifier.NotifyCreatedAsync(order, CancellationToken.None);

        Assert.Equal(DeviceHubGroups.Branch(branchId), clients.CapturedGroupName);
        Assert.Equal(DeviceRealtimeEvents.ShopOrderCreated, clients.Proxy.CapturedMethod);
        Assert.Single(clients.Proxy.CapturedArgs);
        Assert.Same(order, clients.Proxy.CapturedArgs[0]);
    }

    [Fact]
    public async Task NotifyUpdated_SendsToBranchGroup()
    {
        var branchId = Guid.NewGuid();
        var order = new ShopOrderDto(
            Guid.NewGuid(), branchId, Guid.NewGuid(), Guid.NewGuid(), "Alex",
            ShopOrderStatusNames.Placed, new MoneyDto("TJS", 1500),
            Array.Empty<ShopOrderLineDto>(), DateTimeOffset.UnixEpoch, null, null, null, 2);

        var clients = new CapturingHubClients();
        var hubContext = new CapturingHubContext(clients);

        var notifier = new SignalRShopOrderNotifier(hubContext);
        await notifier.NotifyUpdatedAsync(order, CancellationToken.None);

        Assert.Equal(DeviceHubGroups.Branch(branchId), clients.CapturedGroupName);
        Assert.Equal(DeviceRealtimeEvents.ShopOrderUpdated, clients.Proxy.CapturedMethod);
        Assert.Single(clients.Proxy.CapturedArgs);
        Assert.Same(order, clients.Proxy.CapturedArgs[0]);
    }

    private sealed class CapturingHubContext(CapturingHubClients clients) : IHubContext<DeviceHub>
    {
        public IHubClients Clients => clients;

        public IGroupManager Groups => throw new NotSupportedException();
    }

    private sealed class CapturingHubClients : IHubClients
    {
        public CapturingClientProxy Proxy { get; } = new();

        public string? CapturedGroupName { get; private set; }

        public IClientProxy All => throw new NotSupportedException();

        public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Client(string connectionId) => throw new NotSupportedException();

        public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();

        public IClientProxy Group(string groupName)
        {
            CapturedGroupName = groupName;
            return Proxy;
        }

        public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();

        public IClientProxy Groups(IReadOnlyList<string> groupNames) => throw new NotSupportedException();

        public IClientProxy User(string userId) => throw new NotSupportedException();

        public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
    }

    private sealed class CapturingClientProxy : IClientProxy
    {
        public string? CapturedMethod { get; private set; }

        public object?[] CapturedArgs { get; private set; } = [];

        public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
        {
            CapturedMethod = method;
            CapturedArgs = args;
            return Task.CompletedTask;
        }
    }
}
