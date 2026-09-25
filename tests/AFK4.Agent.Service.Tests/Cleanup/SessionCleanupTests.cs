using AFK4.Agent.Service.Cleanup;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests.Cleanup;

public sealed class SessionCleanupTests : IDisposable
{
    private static readonly DateTimeOffset SessionStart = DateTimeOffset.Parse("2026-09-25T18:00:00Z");

    private readonly TemporaryDirectory machine = TemporaryDirectory.Create();

    private string Profile => Path.Combine(machine.Path, "Users", "AFK4 Player");

    private string Steam => Path.Combine(machine.Path, "Steam");

    private string Games => Path.Combine(machine.Path, "Games");

    public void Dispose() => machine.Dispose();

    [Fact]
    public async Task ClosesTheSessionsApps_ThenClearsTheTraces()
    {
        var host = new FakeHost(this) { HasSteamAutoLogin = true };
        host.Running.Add(new SessionProcess(1, "cs2.exe", Path.Combine(Games, "cs2.exe"), SessionStart.AddMinutes(3)));
        host.Running.Add(new SessionProcess(2, "steam.exe", Path.Combine(Steam, "steam.exe"), SessionStart.AddHours(-2)));
        host.Running.Add(new SessionProcess(3, "lghub.exe", Path.Combine(Games, "lghub.exe"), SessionStart.AddHours(-2)));
        var loginUsers = Write(Path.Combine(Steam, "config", "loginusers.vdf"), "\"users\" {}");
        Write(Path.Combine(Steam, "config", "config.vdf"), "\"Steam\"\n{\n\t\"ConnectCache\"\n\t{\n\t\t\"k\"\t\"v\"\n\t}\n\t\"CellID\"\t\"52\"\n}\n");
        var sentry = Write(Path.Combine(Steam, "ssfn123456"), "x");
        var chrome = Write(Path.Combine(Profile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cookies"), "x");
        var telegram = Write(Path.Combine(Profile, "AppData", "Roaming", "Telegram Desktop", "tdata", "key_datas"), "x");
        var save = Write(Path.Combine(Profile, "Documents", "My Games", "Skyrim", "save1.ess"), "x");
        var gameSave = Write(Path.Combine(Steam, "userdata", "1", "730", "local.cfg"), "x");

        var outcome = await Cleanup(host).RunAsync(SessionStart, CancellationToken.None);

        Assert.Equal([1, 2], host.Terminated.Order());
        Assert.Equal(2, outcome.ClosedApps);
        Assert.Equal([SessionTraceNames.Steam, SessionTraceNames.Browsers, SessionTraceNames.Messengers], outcome.Cleared);
        Assert.Empty(outcome.Failed);
        Assert.False(File.Exists(loginUsers));
        Assert.False(File.Exists(sentry));
        Assert.False(Directory.Exists(Path.GetDirectoryName(Path.GetDirectoryName(chrome))));
        Assert.False(File.Exists(telegram));
        Assert.Equal("\"Steam\"\n{\n\t\"CellID\"\t\"52\"\n}\n", File.ReadAllText(Path.Combine(Steam, "config", "config.vdf")));
        // Сохранения — ни в «Документах», ни в userdata Steam — не тронуты.
        Assert.True(File.Exists(save));
        Assert.True(File.Exists(gameSave));
        Assert.Contains((@"Software\Valve\Steam", "AutoLoginUser"), host.DeletedValues);
    }

    [Fact]
    public async Task WaitsForTheClosedAppsToExit_BeforeClearing()
    {
        var host = new FakeHost(this) { ExitsAfterChecks = 3 };
        host.Running.Add(new SessionProcess(2, "steam.exe", Path.Combine(Steam, "steam.exe"), SessionStart.AddMinutes(1)));
        Write(Path.Combine(Steam, "config", "loginusers.vdf"), "x");
        var delays = new List<TimeSpan>();

        await Cleanup(host, delays).RunAsync(SessionStart, CancellationToken.None);

        Assert.Equal(3, delays.Count);
    }

    [Fact]
    public async Task ATurnedOffItem_IsNotCleared()
    {
        var host = new FakeHost(this);
        var chrome = Write(Path.Combine(Profile, "AppData", "Local", "Google", "Chrome", "User Data", "Default", "Cookies"), "x");

        var outcome = await Cleanup(host, clear: [SessionTraceNames.Steam]).RunAsync(SessionStart, CancellationToken.None);

        Assert.True(File.Exists(chrome));
        Assert.DoesNotContain(SessionTraceNames.Browsers, outcome.Cleared);
    }

    [Fact]
    public async Task NothingToClear_IsSaidSo()
    {
        var outcome = await Cleanup(new FakeHost(this)).RunAsync(SessionStart, CancellationToken.None);

        Assert.Equal("closed 0 app(s); nothing to clear", outcome.Describe());
    }

    [Fact]
    public async Task WithoutWindows_OrWithoutAnyoneSignedIn_ItSaysWhyItDidNothing()
    {
        Assert.Equal("cleanup skipped: needs Windows",
            (await Cleanup(new FakeHost(this) { Supported = false }).RunAsync(SessionStart, CancellationToken.None)).Describe());
        Assert.Equal("cleanup skipped: no one is signed in to Windows",
            (await Cleanup(new FakeHost(this) { SignedIn = false }).RunAsync(SessionStart, CancellationToken.None)).Describe());
    }

    private SessionCleanup Cleanup(FakeHost host, List<TimeSpan>? delays = null, IReadOnlyList<string>? clear = null) =>
        new(host, new FixedProtection(clear ?? SessionTraceNames.All), NullLogger<SessionCleanup>.Instance, (delay, _) =>
        {
            delays?.Add(delay);
            return Task.CompletedTask;
        });

    private static string Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    private sealed class FakeHost(SessionCleanupTests machine) : IPlayerSessionHost
    {
        private int checks;

        public bool Supported { get; init; } = true;

        public bool SignedIn { get; init; } = true;

        /// В кусте игрока записан автовход Steam.
        public bool HasSteamAutoLogin { get; init; }

        /// Сколько проверок «ещё работает?» закрытые программы переживают.
        public int ExitsAfterChecks { get; init; }

        public List<SessionProcess> Running { get; } = [];

        public List<int> Terminated { get; } = [];

        public List<(string SubKey, string Name)> DeletedValues { get; } = [];

        public bool IsSupported => Supported;

        public IReadOnlyList<string> ProtectedRoots => [Path.Combine(machine.machine.Path, "Windows")];

        public PlayerSessionUser? ConsoleUser() => SignedIn ? new PlayerSessionUser(1, "S-1-5-21-1", machine.Profile) : null;

        public IReadOnlyList<SessionProcess> Processes(PlayerSessionUser user) => Running;

        public bool TryTerminate(int processId)
        {
            Terminated.Add(processId);
            return true;
        }

        public bool IsRunning(int processId) => Terminated.Contains(processId) && checks++ < ExitsAfterChecks;

        public string? SteamDirectory(PlayerSessionUser user) => Directory.Exists(machine.Steam) ? machine.Steam : null;

        public bool DeleteUserValue(PlayerSessionUser user, string subKey, string valueName)
        {
            DeletedValues.Add((subKey, valueName));
            return HasSteamAutoLogin;
        }

        public bool SetUserDword(PlayerSessionUser user, string subKey, string valueName, int value) => true;
    }

    private sealed class FixedProtection(IReadOnlyList<string> clear) : IProtectionEnforcer
    {
        public IReadOnlyList<BlockedWindowRuleDto> BlockedWindows => [];

        public IReadOnlyList<string> ClearAfterSession => clear;

        public Task ApplyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SyncAsync(int serverVersion, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SessionEnforcementResult.Accepted("ok", "ok"));
    }
}
