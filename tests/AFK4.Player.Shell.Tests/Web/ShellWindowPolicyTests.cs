using AFK4.Player.Shell.Web;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>
/// «Поверх всех» — только на запертом экране: в сессии игра должна выходить вперёд, а в
/// обслуживании окно сжимается в полосу над рабочим столом техника.
/// </summary>
public sealed class ShellWindowPolicyTests
{
    [Theory]
    [InlineData(PlayerShellStateNames.Locked, ShellWindowLayout.Cover)]
    [InlineData(PlayerShellStateNames.Offline, ShellWindowLayout.Cover)]
    [InlineData(PlayerShellStateNames.Error, ShellWindowLayout.Cover)]
    [InlineData(PlayerShellStateNames.Active, ShellWindowLayout.Behind)]
    [InlineData(PlayerShellStateNames.Grace, ShellWindowLayout.Behind)]
    [InlineData(PlayerShellStateNames.Ending, ShellWindowLayout.Behind)]
    [InlineData(PlayerShellStateNames.Maintenance, ShellWindowLayout.Band)]
    public void TheWindowLayout_FollowsTheState(string state, ShellWindowLayout layout)
    {
        Assert.Equal(layout, ShellWindowPolicy.Layout(GameForegroundTrackerTests.State(state)));
    }

    [Theory]
    [InlineData(PlayerShellStateNames.Locked, true)]
    [InlineData(PlayerShellStateNames.Active, false)]
    [InlineData(PlayerShellStateNames.Maintenance, false)]
    public void OnlyALockedScreen_CoversEverything(string state, bool onTop)
    {
        Assert.Equal(onTop, ShellWindowPolicy.ShouldStayOnTop(GameForegroundTrackerTests.State(state)));
    }

    [Fact]
    public void WithoutAWordFromTheAgent_ThePcStaysLocked()
    {
        Assert.Equal(ShellWindowLayout.Cover, ShellWindowPolicy.Layout(null));
    }
}
