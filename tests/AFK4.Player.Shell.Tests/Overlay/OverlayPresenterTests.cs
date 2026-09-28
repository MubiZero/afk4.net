using AFK4.Player.Shell.Overlay;
using AFK4.Player.Shell.Tests.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Overlay;

/// <summary>Поверх игры — сообщение клуба на двенадцать секунд, 10 и 5 минут до конца и последняя минута, пока впереди игра.</summary>
public sealed class OverlayPresenterTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Fact]
    public void AClubMessage_IsShown_ThenGoesAway()
    {
        var message = new ClubMessage("Через пять минут закрываемся", Now);
        var state = GameForegroundTrackerTests.State(PlayerShellStateNames.Active);

        var shown = OverlayPresenter.Decide(state, 1800, shellInFront: false, message, Now.AddSeconds(5));
        var gone = OverlayPresenter.Decide(state, 1800, shellInFront: false, message, Now.AddSeconds(13));

        Assert.Equal(new OverlayContent(OverlayKind.ClubMessage, Text: "Через пять минут закрываемся"), shown);
        Assert.Null(gone);
    }

    [Fact]
    public void TheLastMinute_IsShownOverTheGame_WithTheTimeLeft()
    {
        var content = OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Ending), 42, shellInFront: false, message: null, Now);

        Assert.Equal(new OverlayContent(OverlayKind.LastMinute, RemainingSeconds: 42), content);
    }

    [Fact]
    public void TheLastMinute_IsNotRepeated_WhenTheShellItselfIsInFront()
    {
        Assert.Null(OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Ending), 42, shellInFront: true, message: null, Now));
    }

    [Fact]
    public void TheCountdown_NeverGoesBelowZero()
    {
        var content = OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Ending), -3, shellInFront: false, message: null, Now);

        Assert.Equal(0, content!.RemainingSeconds);
    }

    [Theory]
    [InlineData(600, 10)]
    [InlineData(595, 10)]
    [InlineData(300, 5)]
    [InlineData(291, 5)]
    public void TenAndFiveMinutesBeforeTheEnd_AShortWarningShowsOverTheGame(int left, int minutes)
    {
        var content = OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Active), left, shellInFront: false, message: null, Now);

        Assert.Equal(new OverlayContent(OverlayKind.TimeWarning, RemainingSeconds: left, WarningMinutes: minutes), content);
    }

    [Theory]
    [InlineData(601)]
    [InlineData(590)]
    [InlineData(420)]
    [InlineData(290)]
    public void BetweenTheMarks_TheWarningIsGone(int left)
    {
        Assert.Null(OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Active), left, shellInFront: false, message: null, Now));
    }

    [Fact]
    public void AnOpenTab_HasNoEnd_AndNoWarnings()
    {
        Assert.Null(OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Active), remainingSecondsNow: null, shellInFront: false, message: null, Now));
    }

    [Fact]
    public void ANormalSession_ShowsNothing()
    {
        Assert.Null(OverlayPresenter.Decide(
            GameForegroundTrackerTests.State(PlayerShellStateNames.Active), 1800, shellInFront: false, message: null, Now));
    }
}
