using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceCommandDispatchService(IHubContext<DeviceHub> hubContext, IDeviceCommandStore commandStore, TimeProvider timeProvider) : IDeviceCommandDispatchService
{
    public async Task<DeviceCommandDto> EnqueueAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: request.Type,
            CreatedAtUtc: timeProvider.GetUtcNow(),
            Payload: request.Payload);

        await commandStore.AddPendingAsync(deviceId, command, cancellationToken);

        return command;
    }

    public async Task NotifyAsync(
        Guid deviceId,
        DeviceCommandDto command,
        CancellationToken cancellationToken)
    {
        await hubContext.Clients
            .Group(DeviceHubGroups.Device(deviceId))
            .SendAsync(DeviceRealtimeEvents.DeviceCommand, command, cancellationToken);
    }

    public async Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = await EnqueueAsync(deviceId, request, cancellationToken);
        await NotifyAsync(deviceId, command, cancellationToken);

        return command;
    }
}
