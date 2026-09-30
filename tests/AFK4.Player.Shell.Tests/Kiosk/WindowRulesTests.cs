using AFK4.Player.Shell.Kiosk;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Player.Shell.Tests.Kiosk;

/// <summary>Какие окна хост закрывает по профилю защиты (спека оболочки, §6.3).</summary>
public sealed class WindowRulesTests
{
    private static readonly IReadOnlyList<BlockedWindowRuleDto> Rules =
    [
        new("командная строка", null),
        new(null, "RegEdit_RegEdit"),
        new("Steam", "SDL_app")
    ];

    [Theory]
    [InlineData("Администратор: Командная строка", "ConsoleWindowClass", true)]
    [InlineData("Редактор реестра", "regedit_regedit", true)]
    [InlineData("Steam", "SDL_app", true)]
    public void AMatchingWindow_IsClosed(string title, string className, bool close)
    {
        Assert.Equal(close, WindowRules.ShouldClose(Rules, title, className, ownProcess: false));
    }

    /// <summary>Правило с двумя признаками — это «и», а не «или»: окно Steam другого класса остаётся.</summary>
    [Theory]
    [InlineData("Steam", "vguiPopupWindow")]
    [InlineData("Блокнот", "Notepad")]
    public void AWindowThatMatchesOnlyPartOfARule_Stays(string title, string className)
    {
        Assert.False(WindowRules.ShouldClose(Rules, title, className, ownProcess: false));
    }

    /// <summary>
    /// Приёмка на Windows 11 25H2: cmd.exe из-под игрока открылся в Windows Terminal, и окно с классом
    /// CASCADIA_HOSTING_WINDOW_CLASS пережило правило «ConsoleWindowClass». Клуб, закрывший консоль,
    /// закрыл консоль — в каком бы окне Windows её ни показала.
    /// </summary>
    [Fact]
    public void AConsoleRule_AlsoClosesTheConsoleShownInWindowsTerminal()
    {
        IReadOnlyList<BlockedWindowRuleDto> console = [new(null, "ConsoleWindowClass")];

        Assert.True(WindowRules.ShouldClose(console, "Командная строка", "CASCADIA_HOSTING_WINDOW_CLASS", ownProcess: false));
    }

    [Fact]
    public void TheShellsOwnWindows_AreNeverClosed()
    {
        Assert.False(WindowRules.ShouldClose(Rules, "Командная строка", "ConsoleWindowClass", ownProcess: true));
    }

    [Fact]
    public void WithoutRules_NothingIsClosed()
    {
        Assert.False(WindowRules.ShouldClose([], "Командная строка", "ConsoleWindowClass", ownProcess: false));
        Assert.False(WindowRules.ShouldClose([new BlockedWindowRuleDto(" ", null)], "Командная строка", "ConsoleWindowClass", ownProcess: false));
    }
}
