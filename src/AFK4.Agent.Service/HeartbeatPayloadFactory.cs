using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service;

public static class HeartbeatPayloadFactory
{
    public static DeviceHeartbeatRequest Create(AgentOptions options, bool isLocked, DateTimeOffset observedAtUtc)
    {
        return Create(options, isLocked, observedAtUtc, leaseStore: null);
    }

    public static DeviceHeartbeatRequest Create(
        AgentOptions options,
        bool isLocked,
        DateTimeOffset observedAtUtc,
        ISessionLeaseStore? leaseStore)
    {
        var lease = leaseStore?.Current;

        return new DeviceHeartbeatRequest(
            OrganizationId: options.OrganizationId,
            BranchId: options.BranchId,
            DeviceId: options.DeviceId,
            MachineName: options.MachineName,
            AgentVersion: options.AgentVersion,
            ShellVersion: options.ShellVersion,
            ObservedAtUtc: observedAtUtc,
            IsLocked: isLocked,
            ActiveSessionId: lease?.SessionId,
            ActiveSessionLeaseExpiresAtUtc: lease?.ExpiresAtUtc,
            ActiveSessionLeaseSequence: lease?.Sequence);
    }
}
