using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>«Поверх всех» — только на запертом экране: в сессии игра должна выходить вперёд.</summary>
public sealed class ShellWindowPolicyTests
{
    [Theory]
    [InlineData(PlayerShellStateNames.Locked, true)]
    [InlineData(PlayerShellStateNames.Offline, true)]
    [InlineData(PlayerShellStateNames.Maintenance, true)]
    [InlineData(PlayerShellStateNames.Error, true)]
    [InlineData(PlayerShellStateNames.Active, false)]
    [InlineData(PlayerShellStateNames.Grace, false)]
    [InlineData(PlayerShellStateNames.Ending, false)]
    public void OnlyALockedScreen_StaysOnTop(string state, bool onTop)
    {
        Assert.Equal(onTop, ShellWindowPolicy.ShouldStayOnTop(GameForegroundTrackerTests.State(state)));
    }

    [Fact]
    public void WithoutAWordFromTheAgent_ThePcStaysLocked()
    {
        Assert.True(ShellWindowPolicy.ShouldStayOnTop(null));
    }
}
