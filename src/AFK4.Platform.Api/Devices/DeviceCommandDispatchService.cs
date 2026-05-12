using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceCommandDispatchService(IHubContext<DeviceHub> hubContext, IDeviceCommandStore commandStore) : IDeviceCommandDispatchService
{
    public async Task<DeviceCommandDto> DispatchAsync(
        Guid deviceId,
        CreateDeviceCommandRequest request,
        CancellationToken cancellationToken)
    {
        var command = new DeviceCommandDto(
            CommandId: Guid.NewGuid(),
            Type: request.Type,
            CreatedAtUtc: DateTimeOffset.UtcNow,
            Payload: request.Payload);

        await commandStore.AddPendingAsync(deviceId, command, cancellationToken);

        await hubContext.Clients
            .Group(DeviceHubGroups.Device(deviceId))
            .SendAsync(DeviceRealtimeEvents.DeviceCommand, command, cancellationToken);

        return command;
    }
}
