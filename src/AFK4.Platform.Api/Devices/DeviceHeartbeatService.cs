using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Devices;

public sealed class DeviceHeartbeatService(
    IHubContext<DeviceHub> hubContext,
    PlatformDbContext dbContext) : IDeviceHeartbeatService
{
    public async Task<DeviceHeartbeatResponse> RecordHeartbeatAsync(
        Guid deviceId,
        DeviceHeartbeatRequest request,
        CancellationToken cancellationToken)
    {
        var device = await dbContext.Devices.SingleOrDefaultAsync(
            candidate =>
                candidate.DeviceId == deviceId &&
                candidate.OrganizationId == request.OrganizationId &&
                candidate.BranchId == request.BranchId,
            cancellationToken);

        if (device is not null)
        {
            device.MachineName = request.MachineName;
            device.AgentVersion = request.AgentVersion;
            device.ShellVersion = request.ShellVersion;
            device.LastHeartbeatAtUtc = request.ObservedAtUtc;
            device.IsOnline = true;
            device.IsLocked = request.IsLocked;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var status = new DeviceStatusChangedDto(
            OrganizationId: request.OrganizationId,
            BranchId: request.BranchId,
            DeviceId: deviceId,
            MachineName: request.MachineName,
            IsOnline: true,
            IsLocked: request.IsLocked,
            ObservedAtUtc: request.ObservedAtUtc);

        await hubContext.Clients.All.SendAsync(DeviceRealtimeEvents.DeviceStatusChanged, status, cancellationToken);

        return new DeviceHeartbeatResponse(
            ServerTimeUtc: DateTimeOffset.UtcNow,
            HeartbeatIntervalSeconds: 10,
            Commands: []);
    }
}
