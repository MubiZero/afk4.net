using AFK4.Agent.Service.Cleanup;
using AFK4.Agent.Service.Games;

namespace AFK4.Agent.Service.Tests.Games;

/// <summary>
/// «Мои приложения»: агент помнит запуски игрока из библиотеки и узнаёт, какие процессы к ним
/// относятся — сам запущенный, его потомки и то, что за него запустил уже работавший лаунчер.
/// </summary>
public sealed class LaunchedAppsTests
{
    private static readonly DateTimeOffset Launched = DateTimeOffset.Parse("2026-09-30T10:00:00Z");

    private const string Windows = FakePlayerSessionHost.Windows;
    private const string Games = @"C:\Games";
    private const string Afk4 = FakePlayerSessionHost.Afk4;

    private readonly FakePlayerSessionHost host = new();
    private DateTimeOffset now = Launched;

    private LaunchedApps Registry() => new(host, () => now);

    [Fact]
    public void TheLaunchedProcess_AndItsChildren_BelongToTheApp()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched));
        host.Running.Add(Proc(101, "crashhandler.exe", Games, Launched.AddSeconds(2), parent: 100));
        host.Running.Add(Proc(102, "helper.exe", Games, Launched.AddSeconds(3), parent: 101));

        var running = apps.Running();

        var app = Assert.Single(running);
        Assert.Equal("cs2", app.AppId);
        Assert.Equal([100, 101, 102], app.ProcessIds);
    }

    [Fact]
    public void TheProcessStartedJustBeforeTheLaunchIsRecorded_StillBelongsToIt()
    {
        // Запуск записывается после того, как процесс создан: его время старта чуть раньше записи.
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched.AddSeconds(-1)));

        Assert.Equal([100], Assert.Single(apps.Running()).ProcessIds);
    }

    [Fact]
    public void AProcessTheLaunchDidNotStart_IsNotTheAppsBusiness()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched));
        // Запущен раньше, рядом, без родства — утилита мыши, оболочка, системные процессы.
        host.Running.Add(Proc(7, "lghub.exe", Games, Launched.AddHours(-3)));
        host.Running.Add(Proc(8, "AFK4.Player.Shell.exe", Path.Combine(Afk4, "Player Shell"), Launched.AddHours(-1)));
        host.Running.Add(Proc(9, "explorer.exe", Windows, Launched.AddHours(-1)));

        Assert.Equal([100], Assert.Single(apps.Running()).ProcessIds);
    }

    [Fact]
    public void WhenTheLauncherWasAlreadyRunning_TheGameItStartsBelongsToTheLaunch()
    {
        // Steam -applaunch: запущенный steam.exe передаёт дело уже работающему и выходит, а игру
        // порождает тот, что работал с утра. Родства с нашим процессом нет — есть родство с лаунчером.
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(50, "steam.exe", @"C:\Steam", Launched.AddHours(-2)));
        host.Running.Add(Proc(51, "steamwebhelper.exe", @"C:\Steam", Launched.AddSeconds(4), parent: 50));
        host.Running.Add(Proc(60, "cs2.exe", Games, Launched.AddSeconds(9), parent: 50));
        host.Running.Add(Proc(61, "cs2crash.exe", Games, Launched.AddSeconds(10), parent: 60));

        var app = Assert.Single(apps.Running());

        // Сам Steam не часть игры: закрыв одну игру, мы не закрываем остальные и клиент.
        Assert.Equal([60, 61], app.ProcessIds);
    }

    [Fact]
    public void LauncherChildren_StartedLongBeforeTheLaunch_AreNotCounted()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(50, "steam.exe", @"C:\Steam", Launched.AddHours(-2)));
        host.Running.Add(Proc(55, "dota2.exe", Games, Launched.AddMinutes(-20), parent: 50));

        Assert.Empty(apps.Running());
    }

    [Fact]
    public void TwoGamesFromOneLauncher_AreKeptApart()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        now = Launched.AddMinutes(10);
        apps.Register("dota2", "Dota 2", rootProcessId: 200);
        host.Running.Add(Proc(50, "steam.exe", @"C:\Steam", Launched.AddHours(-2)));
        host.Running.Add(Proc(60, "cs2.exe", Games, Launched.AddSeconds(9), parent: 50));
        host.Running.Add(Proc(70, "dota2.exe", Games, Launched.AddMinutes(10).AddSeconds(9), parent: 50));

        var running = apps.Running();

        Assert.Equal([60], running.Single(app => app.AppId == "cs2").ProcessIds);
        Assert.Equal([70], running.Single(app => app.AppId == "dota2").ProcessIds);
    }

    [Fact]
    public void AnExeFromTheWindowsFolder_LaunchedFromTheLibrary_IsStillTheAppsOwn()
    {
        // Клуб сам поставил его в библиотеку; защищены от закрытия только чужие потомки.
        var apps = Registry();
        apps.Register("notepad", "Notepad", rootProcessId: 100);
        host.Running.Add(Proc(100, "notepad.exe", Path.Combine(Windows, "System32"), Launched));
        host.Running.Add(Proc(101, "conhost.exe", Path.Combine(Windows, "System32"), Launched.AddSeconds(1), parent: 100));

        Assert.Equal([100], Assert.Single(apps.Running()).ProcessIds);
    }

    [Fact]
    public void ARecycledProcessId_IsNotTheLaunchedApp()
    {
        // Игра закрылась, Windows отдала её номер другому процессу, который старше запуска.
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "other.exe", Games, Launched.AddHours(-5)));

        Assert.Empty(apps.Running());
    }

    [Fact]
    public void Close_EndsTheAppWithItsChildren_AndForgetsIt()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        apps.Register("dota2", "Dota 2", rootProcessId: 200);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched));
        host.Running.Add(Proc(101, "child.exe", Games, Launched.AddSeconds(1), parent: 100));
        host.Running.Add(Proc(200, "dota2.exe", Games, Launched));
        var cs2 = apps.Running().Single(app => app.AppId == "cs2");

        Assert.True(apps.Close(cs2.LaunchId));

        Assert.Equal([100, 101], host.Terminated.Order());
        Assert.Equal(["dota2"], apps.Running().Select(app => app.AppId));
    }

    [Fact]
    public void Close_OfAnUnknownLaunch_SaysSo()
    {
        Assert.False(Registry().Close(Guid.NewGuid()));
    }

    [Fact]
    public void AnAppThatAlreadyExited_DisappearsFromTheList()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched));
        Assert.Single(apps.Running());

        host.Running.Clear();
        now = Launched.AddSeconds(5);

        Assert.Empty(apps.Running());
    }

    [Fact]
    public void Clear_ForgetsEveryLaunch()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.Running.Add(Proc(100, "cs2.exe", Games, Launched));

        apps.Clear();

        Assert.Empty(apps.Running());
    }

    [Fact]
    public void WithoutAWindowsSession_NothingIsRunning()
    {
        var apps = Registry();
        apps.Register("cs2", "Counter-Strike 2", rootProcessId: 100);
        host.SignedIn = false;

        Assert.Empty(apps.Running());
    }

    private static SessionProcess Proc(int id, string image, string folder, DateTimeOffset started, int? parent = null) =>
        new(id, image, Path.Combine(folder, image), started, parent);
}
