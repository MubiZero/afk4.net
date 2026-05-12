using AFK4.Platform.Api.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHubGroupsTests
{
    [Fact]
    public void Device_ReturnsStableDeviceGroupName()
    {
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");

        var group = DeviceHubGroups.Device(deviceId);

        Assert.Equal("device:d76eff15-9cf9-4c30-a6d4-c05fd215793f", group);
    }
}
