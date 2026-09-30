using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

/// <summary>Что считается окном приложения, за которым игрок видит экран, а что — служебным окном.</summary>
public sealed class AppWindowRulesTests
{
    private const uint ShellPid = 10;

    private static WindowInfo Window(
        uint pid = 500,
        bool visible = true,
        bool minimized = false,
        bool owned = false,
        bool toolWindow = false,
        bool cloaked = false,
        int width = 1920,
        int height = 1080) =>
        new(new IntPtr(pid * 100), pid, visible, minimized, owned, toolWindow, cloaked, width, height);

    [Fact]
    public void AnOrdinaryVisibleWindow_IsAnAppWindow()
    {
        Assert.True(AppWindowRules.IsAppWindow(Window(), ShellPid));
    }

    [Theory]
    [InlineData(false, false, false, false, false)]
    [InlineData(true, true, false, false, false)]
    [InlineData(true, false, true, false, false)]
    [InlineData(true, false, false, true, false)]
    [InlineData(true, false, false, false, true)]
    public void HiddenMinimisedOwnedToolAndCloakedWindows_AreNotOnTheScreen(
        bool visible, bool minimized, bool owned, bool toolWindow, bool cloaked)
    {
        var window = Window(visible: visible, minimized: minimized, owned: owned, toolWindow: toolWindow, cloaked: cloaked);

        Assert.False(AppWindowRules.IsAppWindow(window, ShellPid));
    }

    [Fact]
    public void TheShellsOwnWindows_AreNotAnApp()
    {
        Assert.False(AppWindowRules.IsAppWindow(Window(pid: ShellPid), ShellPid));
    }

    [Fact]
    public void ATinyHelperWindow_IsNotWhatThePlayerSees()
    {
        Assert.False(AppWindowRules.IsAppWindow(Window(width: 1, height: 1), ShellPid));
    }

    [Fact]
    public void WindowsOfTheGivenProcesses_AreFound_ForReturningToAGame()
    {
        var windows = new[] { Window(pid: 500), Window(pid: 501), Window(pid: 502, minimized: true), Window(pid: 600) };

        var found = AppWindowRules.OfProcesses(windows, [500, 502, 700], ShellPid);

        // Свёрнутое окно игры тоже находится: «Вернуться» разворачивает его.
        Assert.Equal([new IntPtr(50000), new IntPtr(50200)], found.Select(window => window.Handle).Order());
    }
}
