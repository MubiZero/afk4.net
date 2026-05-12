using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHub(ILogger<DeviceHub> logger) : Hub
{
    public async Task RegisterDeviceAsync(DeviceConnectionRequest request)
    {
        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            DeviceHubGroups.Device(request.DeviceId),
            Context.ConnectionAborted);

        await Clients.Caller.SendAsync(
            DeviceRealtimeEvents.DeviceRegistered,
            request.DeviceId,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} registered realtime connection {ConnectionId}.",
            request.DeviceId,
            Context.ConnectionId);
    }

    public async Task ReportCommandResultAsync(DeviceCommandResultDto result)
    {
        await Clients.All.SendAsync(
            DeviceRealtimeEvents.DeviceCommandResult,
            result,
            Context.ConnectionAborted);

        logger.LogInformation(
            "Device {DeviceId} reported command {CommandId} as {Status}.",
            result.DeviceId,
            result.CommandId,
            result.Status);
    }
}
