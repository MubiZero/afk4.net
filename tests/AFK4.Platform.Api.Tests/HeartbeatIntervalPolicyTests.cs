using AFK4.Platform.Api.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class HeartbeatIntervalPolicyTests
{
    [Fact]
    public void Resolve_IdleDevice_ReturnsDefaultInterval()
    {
        var options = new HeartbeatOptions { DefaultIntervalSeconds = 10, ActiveIntervalSeconds = 3 };

        Assert.Equal(10, HeartbeatIntervalPolicy.Resolve(hasPendingCommands: false, options));
    }

    [Fact]
    public void Resolve_PendingCommands_ReturnsActiveInterval()
    {
        var options = new HeartbeatOptions { DefaultIntervalSeconds = 10, ActiveIntervalSeconds = 3 };

        Assert.Equal(3, HeartbeatIntervalPolicy.Resolve(hasPendingCommands: true, options));
    }

    [Fact]
    public void Resolve_ClampsBelowTheFloor()
    {
        // A too-eager active interval can't shorten the cadence into a DoS.
        var options = new HeartbeatOptions { ActiveIntervalSeconds = 1, MinIntervalSeconds = 3 };

        Assert.Equal(3, HeartbeatIntervalPolicy.Resolve(hasPendingCommands: true, options));
    }

    [Fact]
    public void Resolve_ClampsAboveTheCeiling()
    {
        var options = new HeartbeatOptions { DefaultIntervalSeconds = 600, MaxIntervalSeconds = 60 };

        Assert.Equal(60, HeartbeatIntervalPolicy.Resolve(hasPendingCommands: false, options));
    }
}
