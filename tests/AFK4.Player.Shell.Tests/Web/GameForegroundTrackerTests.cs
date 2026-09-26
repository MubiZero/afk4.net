using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>Страница засыпает через секунду после того, как вперёд вышла игра, и просыпается сразу.</summary>
public sealed class GameForegroundTrackerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Fact]
    public void AGameInFront_PutsThePageToSleep_AfterASecond()
    {
        var tracker = new GameForegroundTracker(TimeSpan.FromSeconds(1));

        Assert.Null(tracker.Observe(shellInFront: false, State(PlayerShellStateNames.Active), Now));
        Assert.Null(tracker.Observe(false, State(PlayerShellStateNames.Active), Now.AddMilliseconds(500)));
        Assert.True(tracker.Observe(false, State(PlayerShellStateNames.Active), Now.AddSeconds(1)));
        Assert.Null(tracker.Observe(false, State(PlayerShellStateNames.Active), Now.AddSeconds(2)));
    }

    [Fact]
    public void AQuickAltTab_DoesNotSleepThePage()
    {
        var tracker = new GameForegroundTracker(TimeSpan.FromSeconds(1));

        tracker.Observe(false, State(PlayerShellStateNames.Active), Now);
        tracker.Observe(true, State(PlayerShellStateNames.Active), Now.AddMilliseconds(400));

        Assert.Null(tracker.Observe(false, State(PlayerShellStateNames.Active), Now.AddMilliseconds(1_100)));
    }

    [Fact]
    public void ComingBackToTheShell_WakesThePageAtOnce()
    {
        var tracker = new GameForegroundTracker(TimeSpan.FromSeconds(1));
        tracker.Observe(false, State(PlayerShellStateNames.Active), Now);
        tracker.Observe(false, State(PlayerShellStateNames.Active), Now.AddSeconds(1));

        Assert.False(tracker.Observe(true, State(PlayerShellStateNames.Active), Now.AddSeconds(1.1)));
    }

    [Theory]
    [InlineData(PlayerShellStateNames.Locked)]
    [InlineData(PlayerShellStateNames.Maintenance)]
    [InlineData(PlayerShellStateNames.Offline)]
    public void OnALockedPc_SomethingElseInFront_IsNotAGame(string state)
    {
        var tracker = new GameForegroundTracker(TimeSpan.FromSeconds(1));

        tracker.Observe(false, State(state), Now);

        Assert.Null(tracker.Observe(false, State(state), Now.AddSeconds(5)));
    }

    [Fact]
    public void TheSessionEndingWhileAGameIsInFront_WakesThePage()
    {
        // Сессия кончилась — экран блокировки должен проснуться, даже если игра ещё не закрылась.
        var tracker = new GameForegroundTracker(TimeSpan.FromSeconds(1));
        tracker.Observe(false, State(PlayerShellStateNames.Ending), Now);
        tracker.Observe(false, State(PlayerShellStateNames.Ending), Now.AddSeconds(1));

        Assert.False(tracker.Observe(false, State(PlayerShellStateNames.Locked), Now.AddSeconds(2)));
    }

    internal static PlayerShellStateDto State(string state, int? remainingSeconds = null) => new(
        OrganizationId: Guid.NewGuid(),
        BranchId: Guid.NewGuid(),
        DeviceId: Guid.NewGuid(),
        State: state,
        SessionId: null,
        LeaseExpiresAtUtc: null,
        RemainingSeconds: remainingSeconds,
        IsOnline: true,
        IsGraceMode: false,
        WarningThresholdSeconds: 300,
        Message: state,
        LauncherApps: []);
}
