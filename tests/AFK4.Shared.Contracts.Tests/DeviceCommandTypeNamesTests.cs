using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceCommandTypeNamesTests
{
    [Fact]
    public void SessionCommandNames_AreStableTransportStrings()
    {
        Assert.Equal("lock", DeviceCommandTypeNames.Lock);
        Assert.Equal("unlock", DeviceCommandTypeNames.Unlock);
        Assert.Equal("refresh-session-lease", DeviceCommandTypeNames.RefreshSessionLease);
    }
}
