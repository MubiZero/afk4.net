namespace AFK4.Platform.Api.Devices;

public interface IDeviceConnectionRegistry
{
    void Register(string connectionId, DeviceConnectionIdentity identity);

    DeviceConnectionIdentity? Get(string connectionId);

    void Remove(string connectionId);
}
