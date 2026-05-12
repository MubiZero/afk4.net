using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class HeartbeatPayloadFactory
{
    public static DeviceHeartbeatRequest Create(AgentOptions options, bool isLocked, DateTimeOffset observedAtUtc)
    {
        return new DeviceHeartbeatRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            MachineName: options.MachineName,
            AgentVersion: options.AgentVersion,
            ShellVersion: options.ShellVersion,
            ObservedAtUtc: observedAtUtc,
            IsLocked: isLocked);
    }
}
