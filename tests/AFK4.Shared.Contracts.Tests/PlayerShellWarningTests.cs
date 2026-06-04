using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Tests;

public sealed class PlayerShellWarningTests
{
    [Fact]
    public void GraceState_ClassifiesAsCreditLimit()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Grace, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: true);

        Assert.Equal(PlayerShellWarningKinds.CreditLimit, kind);
    }

    [Fact]
    public void OfflineState_ClassifiesAsConnectivity()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Offline, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.Connectivity, kind);
    }

    [Fact]
    public void ActiveBelowThreshold_ClassifiesAsLowTime()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Active, remainingSeconds: 120, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.LowTime, kind);
    }

    [Fact]
    public void ActiveAboveThreshold_ClassifiesAsNone()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Active, remainingSeconds: 1800, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.None, kind);
    }

    [Fact]
    public void LockedState_ClassifiesAsNone()
    {
        var kind = PlayerShellWarning.Classify(
            PlayerShellStateNames.Locked, remainingSeconds: null, warningThresholdSeconds: 300, isGraceMode: false);

        Assert.Equal(PlayerShellWarningKinds.None, kind);
    }
}
