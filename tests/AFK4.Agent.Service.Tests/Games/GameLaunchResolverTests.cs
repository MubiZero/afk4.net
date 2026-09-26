using AFK4.Agent.Service.Games;
using AFK4.Shared.Contracts.Games;

namespace AFK4.Agent.Service.Tests.Games;

public sealed class GameLaunchResolverTests
{
    private sealed class Locator(bool installed = true) : IGameLauncherLocator
    {
        public string? SteamExecutable() => installed ? @"D:\Steam\steam.exe" : null;

        public string? EpicLauncherExecutable() => installed ? @"C:\Epic\EpicGamesLauncher.exe" : null;

        public string? RiotClientExecutable() => installed ? @"C:\Riot Games\Riot Client\RiotClientServices.exe" : null;

        public string? BattleNetExecutable() => installed ? @"C:\Battle.net\Battle.net.exe" : null;

        public string UrlOpener() => @"C:\Windows\System32\rundll32.exe";
    }

    private static DeviceGameDto Game(string kind, string? target, string? path = null, string? arguments = null) =>
        new("g1", "Игра", null, null, null, kind, target, path, arguments, false);

    [Fact]
    public void Steam_LaunchesTheGameThroughTheClient_WithItsOptions()
    {
        var launch = GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Steam, "730", arguments: "-novid"), new Locator());

        Assert.Equal(new ResolvedLaunch(@"D:\Steam\steam.exe", "-applaunch 730 -novid"), launch);
    }

    [Fact]
    public void Riot_AndBattleNet_GoThroughTheirClients()
    {
        Assert.Equal(
            new ResolvedLaunch(@"C:\Riot Games\Riot Client\RiotClientServices.exe", "--launch-product=valorant --launch-patchline=live"),
            GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Riot, "valorant"), new Locator()));
        Assert.Equal(
            new ResolvedLaunch(@"C:\Battle.net\Battle.net.exe", "--exec=\"launch Pro\""),
            GameLaunchResolver.Resolve(Game(GameLaunchKindNames.BattleNet, "Pro"), new Locator()));
    }

    // В киоске нет проводника — ссылку Epic открывает url.dll, а не explorer.exe.
    [Fact]
    public void Epic_OpensItsLinkWithoutExplorer()
    {
        var launch = GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Epic, "Fortnite"), new Locator());

        Assert.Equal(@"C:\Windows\System32\rundll32.exe", launch!.ExecutablePath);
        Assert.Equal("url.dll,FileProtocolHandler com.epicgames.launcher://apps/Fortnite?action=launch&silent=true", launch.Arguments);
    }

    [Fact]
    public void AnOwnExePath_ReplacesTheLauncher()
    {
        var launch = GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Steam, "570", path: @"E:\Games\dota2.exe", arguments: "-console"), new Locator(installed: false));

        Assert.Equal(new ResolvedLaunch(@"E:\Games\dota2.exe", "-console"), launch);
    }

    [Fact]
    public void AnExeFromTheCatalog_UsesItsDefaultPath()
    {
        Assert.Equal(new ResolvedLaunch(@"C:\Games\own.exe", ""), GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Executable, @"C:\Games\own.exe"), new Locator()));
    }

    // Лаунчера на ПК нет — запуска нет; плитка станет недоступной, а не упадёт при нажатии.
    [Theory]
    [InlineData(GameLaunchKindNames.Steam, "730")]
    [InlineData(GameLaunchKindNames.Epic, "Fortnite")]
    [InlineData(GameLaunchKindNames.Riot, "valorant")]
    [InlineData(GameLaunchKindNames.BattleNet, "WoW")]
    public void WithoutTheLauncher_ThereIsNoLaunch(string kind, string target)
    {
        Assert.Null(GameLaunchResolver.Resolve(Game(kind, target), new Locator(installed: false)));
    }

    [Fact]
    public void WithoutATarget_ThereIsNoLaunch()
    {
        Assert.Null(GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Steam, null), new Locator()));
        Assert.Null(GameLaunchResolver.Resolve(Game(GameLaunchKindNames.Executable, null), new Locator()));
    }
}
