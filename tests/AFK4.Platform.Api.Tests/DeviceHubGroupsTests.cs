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

    [Fact]
    public void Branch_ReturnsStableBranchGroupName()
    {
        var branchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

        var group = DeviceHubGroups.Branch(branchId);

        Assert.Equal("branch:acfc0212-967f-4d84-94be-9003387b09c2", group);
    }
}
