using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerShellLockPolicyTests
{
    private static PlayerShellStateDto State(string state, int? remaining = 1200) =>
        new(
            OrganizationId: Guid.NewGuid(),
            BranchId: Guid.NewGuid(),
            DeviceId: Guid.NewGuid(),
            State: state,
            SessionId: Guid.NewGuid(),
            LeaseExpiresAtUtc: DateTimeOffset.UtcNow.AddMinutes(20),
            RemainingSeconds: remaining,
            IsOnline: true,
            IsGraceMode: false,
            WarningThresholdSeconds: 300,
            Message: "ok",
            LauncherApps: []);

    [Fact]
    public void NoState_IsLocked()
    {
        // Until the pipe delivers authoritative state, assume locked.
        Assert.True(PlayerShellLockPolicy.IsLocked(state: null));
    }

    [Fact]
    public void LockedState_IsLocked()
    {
        Assert.True(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Locked)));
    }

    [Fact]
    public void ActiveState_IsNotLocked()
    {
        Assert.False(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Active)));
    }

    [Fact]
    public void OfflineWithLease_IsNotLocked()
    {
        // Offline but lease still valid: keep playing.
        Assert.False(PlayerShellLockPolicy.IsLocked(State(PlayerShellStateNames.Active) with { IsOnline = false }));
    }
}
