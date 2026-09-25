using AFK4.Player.Shell.Kiosk;
using AFK4.Player.Shell.Tests.Web;
using AFK4.Shared.Contracts.Shell;
using static AFK4.Player.Shell.Kiosk.KeyboardBlockPolicy;

namespace AFK4.Player.Shell.Tests.Kiosk;

/// <summary>Таблица перехвата клавиш из спеки оболочки, §6.2.</summary>
public sealed class KeyboardBlockPolicyTests
{
    private const int VkA = 0x41;

    private static readonly KeyStroke Win = new(VkLeftWin, false, false, false);
    private static readonly KeyStroke RightWin = new(VkRightWin, false, false, false);
    private static readonly KeyStroke AltTab = new(VkTab, Alt: true, Ctrl: false, Shift: false);
    private static readonly KeyStroke AltEsc = new(VkEscape, Alt: true, Ctrl: false, Shift: false);
    private static readonly KeyStroke CtrlEsc = new(VkEscape, Alt: false, Ctrl: true, Shift: false);
    private static readonly KeyStroke AltF4 = new(VkF4, Alt: true, Ctrl: false, Shift: false);
    private static readonly KeyStroke TaskManager = new(VkEscape, Alt: false, Ctrl: true, Shift: true);

    public static TheoryData<KeyStroke> LockedCombos => new() { Win, RightWin, AltTab, AltEsc, CtrlEsc, AltF4, TaskManager };

    [Theory]
    [MemberData(nameof(LockedCombos))]
    public void ALockedPc_SwallowsEveryWayOut(KeyStroke key)
    {
        Assert.True(ShouldBlock(ShellKeyMode.Locked, key, shellInFront: true));
    }

    [Theory]
    [InlineData(VkA, false, false)]
    [InlineData(VkTab, false, false)]
    [InlineData(VkTab, false, true)]
    [InlineData(VkEscape, false, false)]
    [InlineData(VkF4, false, false)]
    public void ALockedPc_StillTakesTypingInFields(int key, bool ctrl, bool shift)
    {
        Assert.False(ShouldBlock(ShellKeyMode.Locked, new KeyStroke(key, false, ctrl, shift), shellInFront: true));
    }

    [Fact]
    public void InASession_TheStartMenuStaysShut()
    {
        Assert.True(ShouldBlock(ShellKeyMode.Session, Win, shellInFront: false));
        Assert.True(ShouldBlock(ShellKeyMode.Session, RightWin, shellInFront: false));
        Assert.True(ShouldBlock(ShellKeyMode.Session, CtrlEsc, shellInFront: false));
    }

    [Fact]
    public void InASession_AltTabSwitchesBetweenTheGameAndTheShell()
    {
        Assert.False(ShouldBlock(ShellKeyMode.Session, AltTab, shellInFront: true));
        Assert.False(ShouldBlock(ShellKeyMode.Session, AltTab, shellInFront: false));
    }

    [Fact]
    public void InASession_AltF4ClosesTheGameButNotTheShell()
    {
        Assert.False(ShouldBlock(ShellKeyMode.Session, AltF4, shellInFront: false));
        Assert.True(ShouldBlock(ShellKeyMode.Session, AltF4, shellInFront: true));
    }

    [Theory]
    [MemberData(nameof(LockedCombos))]
    public void InMaintenance_TheTechnicianGetsEveryKey(KeyStroke key)
    {
        Assert.False(ShouldBlock(ShellKeyMode.Maintenance, key, shellInFront: true));
    }

    [Theory]
    [InlineData(PlayerShellStateNames.Locked, ShellKeyMode.Locked)]
    [InlineData(PlayerShellStateNames.Offline, ShellKeyMode.Locked)]
    [InlineData(PlayerShellStateNames.Error, ShellKeyMode.Locked)]
    [InlineData(PlayerShellStateNames.Active, ShellKeyMode.Session)]
    [InlineData(PlayerShellStateNames.Grace, ShellKeyMode.Session)]
    [InlineData(PlayerShellStateNames.Maintenance, ShellKeyMode.Maintenance)]
    public void TheModeFollowsTheAgentState(string state, ShellKeyMode mode)
    {
        Assert.Equal(mode, ModeFor(GameForegroundTrackerTests.State(state)));
    }

    [Fact]
    public void WithoutAWordFromTheAgent_ThePcStaysLocked()
    {
        Assert.Equal(ShellKeyMode.Locked, ModeFor(null));
    }
}
