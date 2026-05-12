namespace AFK4.Platform.Api.Devices;

public static class DeviceHubGroups
{
    public static string Device(Guid deviceId) => $"device:{deviceId:D}";
}
