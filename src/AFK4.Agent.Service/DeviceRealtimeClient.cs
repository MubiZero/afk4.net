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

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger)
        : this(
            options,
            commandHandler,
            logger,
            new SignalRDeviceHubConnection(
                new HubConnectionBuilder()
                    .WithUrl(new Uri(options.Value.PlatformBaseUrl, "/hubs/devices"))
                    .WithAutomaticReconnect()
                    .Build()))
    {
    }

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger,
        IDeviceHubConnection connection)
    {
        this.options = options.Value;
        this.commandHandler = commandHandler;
        this.logger = logger;
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
        var request = DeviceConnectionRequestFactory.Create(options, DateTimeOffset.UtcNow);
        return connection.InvokeAsync(DeviceRealtimeMethods.RegisterDeviceAsync, request, cancellationToken);
    }

    private async Task HandleCommandAsync(DeviceCommandDto command)
    {
        var result = await commandHandler.HandleAsync(command, CancellationToken.None);
        await connection.InvokeAsync(DeviceRealtimeMethods.ReportCommandResultAsync, result);

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
