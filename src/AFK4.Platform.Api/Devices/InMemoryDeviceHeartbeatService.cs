using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;

namespace AFK4.Platform.Api.Devices;

public sealed class InMemoryDeviceHeartbeatService(IHubContext<DeviceHub> hubContext) : IDeviceHeartbeatService
{
    public async Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(
        Guid deviceId,
        DeviceHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var status = new DeviceStatusChangedDto(
            OrganizationId: request.OrganizationId,
            BranchId: request.BranchId,
            DeviceId: deviceId,
            MachineName: request.MachineName,
            IsOnline: true,
            IsLocked: request.IsLocked,
            ObservedAtUtc: request.ObservedAtUtc);

        await hubContext.Clients.All.SendAsync("deviceStatusChanged", status, cancellationToken);

        return new DeviceHeartbeatResponse(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            HeartbeatIntervalSeconds: 10,
            Commands: []);
    }
}
