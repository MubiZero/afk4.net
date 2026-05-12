using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class DeviceRealtimeClient : IAsyncDisposable
{
    private readonly AgentOptions options;
    private readonly IDeviceCommandHandler commandHandler;
    private readonly ILogger<DeviceRealtimeClient> logger;
    private readonly HubConnection connection;

    public DeviceRealtimeClient(
        IOptions<AgentOptions> options,
        IDeviceCommandHandler commandHandler,
        ILogger<DeviceRealtimeClient> logger)
    {
        this.options = options.Value;
        this.commandHandler = commandHandler;
        this.logger = logger;
        connection = new HubConnectionBuilder()
            .WithUrl(new Uri(this.options.PlatformBaseUrl, "/hubs/devices"))
            .WithAutomaticReconnect()
            .Build();

        connection.On<DeviceCommandDto>(DeviceRealtimeEvents.DeviceCommand, HandleCommandAsync);
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await connection.StartAsync(cancellationToken);

        var request = DeviceConnectionRequestFactory.Create(options, DateTimeOffset.UtcNow);
        await connection.InvokeAsync(DeviceRealtimeMethods.RegisterDeviceAsync, request, cancellationToken);

        logger.LogInformation("Realtime device channel connected for {DeviceId}.", options.DeviceId);
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
