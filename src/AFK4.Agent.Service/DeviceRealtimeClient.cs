using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public interface IDeviceRealtimeClient : IAsyncDisposable
{
    Task StartAsync(CancellationToken cancellationToken);
}

public interface IDeviceHubConnection : IAsyncDisposable
{
    event Func<string?, Task>? Reconnected;

    IDisposable On<T>(string methodName, Func<T, Task> handler);

    Task StartAsync(CancellationToken cancellationToken);

    Task InvokeAsync(string methodName, object? argument, CancellationToken cancellationToken = default);
}

public sealed class DeviceRealtimeClient : IDeviceRealtimeClient
{
    private readonly AgentOptions options;
    private readonly IDeviceCommandHandler commandHandler;
    private readonly ILogger<DeviceRealtimeClient> logger;
    private readonly IDeviceHubConnection connection;
    private readonly ISessionLeaseStore? leaseStore;
    private readonly ICommandResultOutbox? commandResultOutbox;

    /// null у тех, кто про смену ключа не знает (тесты хаба): тогда берётся ключ из конфига.
    private readonly IDeviceCredentialStore? credentialStore;

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        ISessionLeaseStore leaseStore,
        ICommandResultOutbox commandResultOutbox,
        IDeviceCredentialStore credentialStore)
        : this(
            options,
            commandHandler,
            logger,
            leaseStore,
            commandResultOutbox,
            new SignalRDeviceHubConnection(
                new HubConnectionBuilder()
                    .WithUrl(new Uri(options.Value.PlatformBaseUrl, "/hubs/devices"))
                    .WithAutomaticReconnect()
                    .Build()),
            credentialStore)
    {
    }

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        IDeviceHubConnection connection)
        : this(options, commandHandler, logger, leaseStore: null, commandResultOutbox: null, connection)
    {
    }

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        ISessionLeaseStore? leaseStore,
        ICommandResultOutbox? commandResultOutbox,
        IDeviceHubConnection connection,
        IDeviceCredentialStore? credentialStore = null)
    {
        this.credentialStore = credentialStore;
        this.options = options.Value;
        this.commandHandler = commandHandler;
        this.logger = logger;
        this.leaseStore = leaseStore;
        this.commandResultOutbox = commandResultOutbox;
        this.connection = connection;

        this.connection.On<DeviceCommandDto>(DeviceRealtimeEvents.DeviceCommand, HandleCommandAsync);
        this.connection.Reconnected += HandleReconnectedAsync;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await connection.StartAsync(cancellationToken);
        await RegisterDeviceAsync(cancellationToken);

        logger.LogInformation("Realtime device channel connected for {DeviceId}.", options.DeviceId);
    }

    private Task HandleReconnectedAsync(string? connectionId)
    {
        return RegisterDeviceAsync(CancellationToken.None);
    }

    private Task RegisterDeviceAsync(CancellationToken cancellationToken)
    {
        var request = DeviceConnectionRequestFactory.Create(
            options, DateTimeOffset.UtcNow, leaseStore, credentialStore?.Current);
        return connection.InvokeAsync(DeviceRealtimeMethods.RegisterDeviceAsync, request, cancellationToken);
    }

    private async Task HandleCommandAsync(DeviceCommandDto command)
    {
        // Persist the result before delivery so SignalR shares the same durable return path as the HTTP
        // heartbeat (spec §6.4). If the realtime invoke fails, the result stays queued and the heartbeat
        // drain re-POSTs it; the backend dedupes on CommandId.
        var result = await commandHandler.HandleAsync(command, CancellationToken.None);
        commandResultOutbox?.Enqueue(result);

        await connection.InvokeAsync(DeviceRealtimeMethods.ReportCommandResultAsync, result);
        commandResultOutbox?.Acknowledge(result.CommandId);

        logger.LogInformation(
            "Command {CommandId} acknowledged as {Status}.",
            command.CommandId,
            result.Status);
    }

    public async ValueTask DisposeAsync()
    {
        await connection.DisposeAsync();
    }
}

internal sealed class SignalRDeviceHubConnection(HubConnection connection) : IDeviceHubConnection
{
    public event Func<string?, Task>? Reconnected
    {
        add => connection.Reconnected += value;
        remove => connection.Reconnected -= value;
    }

    public IDisposable On<T>(string methodName, Func<T, Task> handler)
    {
        return connection.On(methodName, handler);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        return connection.StartAsync(cancellationToken);
    }

    public Task InvokeAsync(string methodName, object? argument, CancellationToken cancellationToken = default)
    {
        return connection.InvokeAsync(methodName, argument, cancellationToken);
    }

    public ValueTask DisposeAsync()
    {
        return connection.DisposeAsync();
    }
}
