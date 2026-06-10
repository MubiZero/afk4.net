using AFK4.Agent.Service;

namespace AFK4.Agent.Service.Tests;

public sealed class ClockDriftCheckTests
{
    private static readonly DateTimeOffset ServerNow = DateTimeOffset.Parse("2026-05-14T10:00:00Z");

    [Fact]
    public void Drift_AgentBehindServer_IsPositive()
    {
        var drift = ClockDriftCheck.Drift(ServerNow, ServerNow.AddSeconds(-45));

        Assert.Equal(TimeSpan.FromSeconds(45), drift);
    }

    [Fact]
    public void Drift_AgentAheadOfServer_IsNegative()
    {
        var drift = ClockDriftCheck.Drift(ServerNow, ServerNow.AddSeconds(45));

        Assert.Equal(TimeSpan.FromSeconds(-45), drift);
    }

    [Fact]
    public void IsExcessive_WithinThreshold_IsFalse()
    {
        var drift = ClockDriftCheck.Drift(ServerNow, ServerNow.AddSeconds(10));

        Assert.False(ClockDriftCheck.IsExcessive(drift, ClockDriftCheck.WarningThreshold));
    }

    [Fact]
    public void IsExcessive_BeyondThresholdEitherDirection_IsTrue()
    {
        var behind = ClockDriftCheck.Drift(ServerNow, ServerNow.AddSeconds(-31));
        var ahead = ClockDriftCheck.Drift(ServerNow, ServerNow.AddSeconds(31));

        Assert.True(ClockDriftCheck.IsExcessive(behind, ClockDriftCheck.WarningThreshold));
        Assert.True(ClockDriftCheck.IsExcessive(ahead, ClockDriftCheck.WarningThreshold));
    }

    [Fact]
    public void IsExcessive_ExactlyAtThreshold_IsTrue()
    {
        Assert.True(ClockDriftCheck.IsExcessive(ClockDriftCheck.WarningThreshold, ClockDriftCheck.WarningThreshold));
    }
}
