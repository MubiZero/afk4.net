using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Sessions;

public interface IHeartbeatSessionCommandPlanner
{
    Task<IReadOnlyList<HeartbeatSessionCommandPlan>> PlanAsync(
        Guid deviceId,
        DeviceHeartbeatRequest heartbeat,
        CancellationToken cancellationToken);
}
