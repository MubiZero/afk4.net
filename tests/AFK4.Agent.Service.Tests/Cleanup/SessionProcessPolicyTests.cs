using AFK4.Agent.Service.Cleanup;

namespace AFK4.Agent.Service.Tests.Cleanup;

public sealed class SessionProcessPolicyTests
{
    private static readonly DateTimeOffset SessionStart = DateTimeOffset.Parse("2026-09-25T18:00:00Z");
    private static readonly string Windows = Path.Combine(Path.GetTempPath(), "Windows");
    private static readonly string Afk4 = Path.Combine(Path.GetTempPath(), "Program Files", "AFK4");
    private static readonly string Games = Path.Combine(Path.GetTempPath(), "Games");
    private static readonly IReadOnlyList<string> Protected = [Windows, Afk4];
    private static readonly IReadOnlySet<string> Launchers = new HashSet<string>(["steam.exe"], StringComparer.OrdinalIgnoreCase);

    private static SessionProcess Process(string directory, string image, DateTimeOffset? startedAt) =>
        new(42, image, Path.Combine(directory, image), startedAt);

    [Fact]
    public void AGameStartedDuringTheSession_IsClosed()
    {
        Assert.True(SessionProcessPolicy.ShouldClose(Process(Games, "cs2.exe", SessionStart.AddMinutes(5)), SessionStart, Launchers, Protected));
    }

    // Утилиты мыши и подсветки стартуют при входе в Windows: закрыть их — оставить клуб без них до
    // перезагрузки.
    [Fact]
    public void WhatRanBeforeTheSession_IsLeftAlone()
    {
        Assert.False(SessionProcessPolicy.ShouldClose(Process(Games, "lghub.exe", SessionStart.AddHours(-3)), SessionStart, Launchers, Protected));
    }

    // Steam, запущенный с утра, держит файл входа и перепишет его при выходе — его закрывают всегда.
    [Fact]
    public void ALauncherFromTheCatalog_IsClosedWhenEverItStarted()
    {
        Assert.True(SessionProcessPolicy.ShouldClose(Process(Games, "Steam.exe", SessionStart.AddHours(-3)), SessionStart, Launchers, Protected));
        Assert.True(SessionProcessPolicy.ShouldClose(Process(Games, "steam.exe", null), sessionStartedAtUtc: null, Launchers, Protected));
    }

    [Fact]
    public void WindowsAndAfk4_AreNeverClosed()
    {
        Assert.False(SessionProcessPolicy.ShouldClose(Process(Path.Combine(Windows, "System32"), "sihost.exe", SessionStart.AddMinutes(1)), SessionStart, Launchers, Protected));
        Assert.False(SessionProcessPolicy.ShouldClose(Process(Path.Combine(Afk4, "Player Shell"), "AFK4.Player.Shell.exe", SessionStart.AddMinutes(1)), SessionStart, Launchers, Protected));
    }

    [Fact]
    public void AProcessWhosePathIsUnknown_IsLeftAlone()
    {
        Assert.False(SessionProcessPolicy.ShouldClose(new SessionProcess(42, "steam.exe", null, SessionStart.AddMinutes(1)), SessionStart, Launchers, Protected));
    }

    [Fact]
    public void WithoutASessionStart_OnlyTheCatalogIsClosed()
    {
        Assert.False(SessionProcessPolicy.ShouldClose(Process(Games, "cs2.exe", SessionStart.AddMinutes(5)), sessionStartedAtUtc: null, Launchers, Protected));
    }
}
