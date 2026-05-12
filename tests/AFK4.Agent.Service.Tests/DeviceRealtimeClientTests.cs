using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class DeviceRealtimeClientTests
{
    [Fact]
    public async Task StartAsync_RegistersDeviceInitiallyAndAfterReconnect()
    {
        var connection = new CapturingDeviceHubConnection();
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001"
        });

        var client = new DeviceRealtimeClient(
            options,
            new NoOpDeviceCommandHandler(options.Value),
            NullLogger<DeviceRealtimeClient>.Instance,
            connection);

        await client.StartAsync(CancellationToken.None);
        await connection.SimulateReconnectedAsync();

        var registrations = connection.Invocations
            .Where(invocation => invocation.MethodName == DeviceRealtimeMethods.RegisterDeviceAsync)
            .ToArray();

        Assert.Equal(2, registrations.Length);
        Assert.All(registrations, invocation =>
        {
            var request = Assert.IsType<DeviceConnectionRequest>(invocation.Argument);
            Assert.Equal(options.Value.OrganizationId, request.OrganizationId);
            Assert.Equal(options.Value.BranchId, request.BranchId);
            Assert.Equal(options.Value.DeviceId, request.DeviceId);
            Assert.Equal("PC-001", request.MachineName);
        });
    }

    private sealed class NoOpDeviceCommandHandler(AgentOptions options) : IDeviceCommandHandler
    {
        public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeviceCommandResultDto(
                options.OrganizationId,
                options.BranchId,
                options.DeviceId,
                command.CommandId,
                "Accepted",
                "Accepted",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class CapturingDeviceHubConnection : IDeviceHubConnection
    {
        private Func<string?, Task>? reconnected;

        public List<Invocation> Invocations { get; } = [];

        public event Func<string?, Task>? Reconnected
        {
            add => reconnected += value;
            remove => reconnected -= value;
        }

        public IDisposable On<T>(string methodName, Func<T, Task> handler)
        {
            return EmptyDisposable.Instance;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task InvokeAsync(string methodName, object? argument, CancellationToken cancellationToken = default)
        {
            Invocations.Add(new Invocation(methodName, argument));
            return Task.CompletedTask;
        }

        public Task SimulateReconnectedAsync()
        {
            return reconnected?.Invoke("connection-id") ?? Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed record Invocation(string MethodName, object? Argument);

    private sealed class EmptyDisposable : IDisposable
    {
        public static readonly EmptyDisposable Instance = new();

        public void Dispose()
        {
        }
    }
}
