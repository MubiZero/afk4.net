using AFK4.Agent.Service.Network;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;

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
        ISessionLeaseStore? leaseStore,
        NetworkIdentity? network = null,
        bool? kioskInstalled = null,
        bool? gameCacheReachable = null,
        GameCacheServer? gameCacheServer = null)
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
            ActiveSessionLeaseSequence: lease?.Sequence,
            NetworkMacAddress: network?.MacAddress,
            NetworkSubnet: network?.Subnet,
            NetworkBroadcastAddress: network?.BroadcastAddress,
            NetworkAddress: network?.Address,
            ActiveSessionLeaseOffline: lease is null ? null : OfflineLeaseSigning.TryGetKeyId(lease.SignatureAlgorithm, out _),
            KioskInstalled: kioskInstalled,
            GameCacheReachable: gameCacheReachable,
            GameCacheServing: gameCacheServer?.Serving,
            GameCacheStoredBytes: gameCacheServer is { Serving: true } ? gameCacheServer.StoredBytes : null);
    }
}
