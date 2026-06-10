using AFK4.Platform.Api.Devices;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandDispatchServiceTests
{
    [Fact]
    public async Task EnqueueAsync_SavesPendingCommandWithoutSendingRealtimeNotification()
    {
        var clients = new CapturingHubClients();
        var hubContext = new CapturingHubContext(clients);
        var commandStore = new InMemoryDeviceCommandStore();
        var service = new DeviceCommandDispatchService(hubContext, commandStore, TimeProvider.System);
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var request = new CreateDeviceCommandRequest(
            Type: "unlock",
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "session-start"
            });

        var command = await service.EnqueueAsync(deviceId, request, CancellationToken.None);

        Assert.Null(clients.CapturedGroupName);
        Assert.Null(clients.Proxy.CapturedMethod);
        Assert.Equal("unlock", command.Type);
        Assert.Equal("session-start", command.Payload["reason"]);

        var status = await commandStore.GetAsync(deviceId, command.CommandId, CancellationToken.None);
        Assert.NotNull(status);
        Assert.Equal("Pending", status.Status);
    }

    [Fact]
    public async Task NotifyAsync_SendsAlreadyCreatedCommandToDeviceGroup()
    {
        var clients = new CapturingHubClients();
        var hubContext = new CapturingHubContext(clients);
        var commandStore = new InMemoryDeviceCommandStore();
        var service = new DeviceCommandDispatchService(hubContext, commandStore, TimeProvider.System);
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var command = new DeviceCommandDto(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            "refresh-session-lease",
            DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            new Dictionary<string, string>
            {
                ["reason"] = "session-extend"
            });

        await service.NotifyAsync(deviceId, command, CancellationToken.None);

        Assert.Equal("device:d76eff15-9cf9-4c30-a6d4-c05fd215793f", clients.CapturedGroupName);
        Assert.Equal(DeviceRealtimeEvents.DeviceCommand, clients.Proxy.CapturedMethod);
        var sentCommand = Assert.IsType<DeviceCommandDto>(Assert.Single(clients.Proxy.CapturedArgs));
        Assert.Same(command, sentCommand);
    }

    [Fact]
    public async Task DispatchAsync_SendsReturnedCommandToDeviceGroup()
    {
        var clients = new CapturingHubClients();
        var hubContext = new CapturingHubContext(clients);
        var commandStore = new InMemoryDeviceCommandStore();
        var service = new DeviceCommandDispatchService(hubContext, commandStore, TimeProvider.System);
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var request = new CreateDeviceCommandRequest(
            Type: "lock",
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "operator-request"
            });

        var command = await service.DispatchAsync(deviceId, request, CancellationToken.None);

        Assert.Equal("device:d76eff15-9cf9-4c30-a6d4-c05fd215793f", clients.CapturedGroupName);
        Assert.Equal(DeviceRealtimeEvents.DeviceCommand, clients.Proxy.CapturedMethod);
        var sentCommand = Assert.IsType<DeviceCommandDto>(Assert.Single(clients.Proxy.CapturedArgs));
        Assert.Same(command, sentCommand);
        Assert.Equal("lock", command.Type);
        Assert.Equal("operator-request", command.Payload["reason"]);

        var status = await commandStore.GetAsync(deviceId, command.CommandId, CancellationToken.None);
        Assert.NotNull(status);
        Assert.Equal("Pending", status.Status);
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
