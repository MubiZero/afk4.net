using System.Collections.Concurrent;

namespace AFK4.Platform.Api.Devices;

public sealed class InMemoryDeviceConnectionRegistry : IDeviceConnectionRegistry
{
    private readonly ConcurrentDictionary<string, DeviceConnectionIdentity> connections = new(StringComparer.Ordinal);

    public void Register(string connectionId, DeviceConnectionIdentity identity)
    {
        connections[connectionId] = identity;
    }

    public DeviceConnectionIdentity? Get(string connectionId)
    {
        connections.TryGetValue(connectionId, out var identity);
        return identity;
    }

    public void Remove(string connectionId)
    {
        connections.TryRemove(connectionId, out _);
    }
}
