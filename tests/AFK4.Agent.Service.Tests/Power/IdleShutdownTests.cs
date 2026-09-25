using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Games;
using AFK4.Agent.Service.Power;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests.Power;

public sealed class IdleShutdownTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-25T20:00:00Z");

    [Fact]
    public void AFreePc_NobodyTouches_TurnsOffAfterTheClubsMinutes()
    {
        var fixture = new Fixture(idleMinutes: 30);

        fixture.CheckAt(Start);
        fixture.CheckAt(Start.AddMinutes(29));
        Assert.Empty(fixture.Power.Scheduled);

        fixture.CheckAt(Start.AddMinutes(30));
        fixture.CheckAt(Start.AddMinutes(31));

        var shutdown = Assert.Single(fixture.Power.Scheduled);
        Assert.Equal(MachinePowerAction.Shutdown, shutdown.Action);
        Assert.Equal(IdleShutdownPolicy.Warning, shutdown.Delay);
    }

    // Человек у ПК вводит номер — простой не считается, хотя сессии ещё нет.
    [Fact]
    public void SomeoneAtThePc_KeepsItOn()
    {
        var fixture = new Fixture(idleMinutes: 30);
        fixture.CheckAt(Start);

        fixture.Presence.Record(Start.AddMinutes(25));
        fixture.CheckAt(Start.AddMinutes(40));

        Assert.Empty(fixture.Power.Scheduled);
        fixture.CheckAt(Start.AddMinutes(55));
        Assert.Single(fixture.Power.Scheduled);
    }

    [Fact]
    public void ToutchingTheMouseInTheLastMinute_CancelsTheShutdown()
    {
        var fixture = new Fixture(idleMinutes: 30);
        fixture.CheckAt(Start);
        fixture.CheckAt(Start.AddMinutes(30));

        fixture.Presence.Record(Start.AddMinutes(30).AddSeconds(20));
        fixture.CheckAt(Start.AddMinutes(30).AddSeconds(30));

        Assert.Equal(1, fixture.Power.Cancels);
        // Простой начался заново: снова полчаса, а не сразу.
        fixture.CheckAt(Start.AddMinutes(45));
        Assert.Single(fixture.Power.Scheduled);
    }

    [Theory]
    [InlineData(PlayerShellStateNames.Active)]
    [InlineData(PlayerShellStateNames.Maintenance)]
    public void DuringASession_OrMaintenance_NothingTurnsOff(string state)
    {
        var fixture = new Fixture(idleMinutes: 30);
        fixture.Runtime.Set(state);

        fixture.CheckAt(Start);
        fixture.CheckAt(Start.AddHours(3));

        Assert.Empty(fixture.Power.Scheduled);
    }

    [Fact]
    public void WithoutTheSetting_NothingTurnsOff()
    {
        var fixture = new Fixture(idleMinutes: null);

        fixture.CheckAt(Start);
        fixture.CheckAt(Start.AddDays(1));

        Assert.Empty(fixture.Power.Scheduled);
    }

    private sealed class Fixture
    {
        private readonly MovableTime time = new(Start);
        private readonly IdleShutdownMonitor monitor;

        public Fixture(int? idleMinutes)
        {
            monitor = new IdleShutdownMonitor(Runtime, new FixedProfile(idleMinutes), Presence, Power, time, NullLogger<IdleShutdownMonitor>.Instance);
        }

        public FakeRuntime Runtime { get; } = new();
        public PlayerPresence Presence { get; } = new();
        public RecordingPower Power { get; } = new();

        public void CheckAt(DateTimeOffset at)
        {
            time.Now = at;
            monitor.Check();
        }
    }

    internal sealed class FakeRuntime : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = AgentRuntimeState.Locked(Start);

        public void Set(string state) => Current = Current with { State = state };

        public void Save(AgentRuntimeState state) => Current = state;

        public void MarkLocked(DateTimeOffset observedAtUtc) => Current = AgentRuntimeState.Locked(observedAtUtc);

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc) => Current = AgentRuntimeState.Active(lease, observedAtUtc);
    }

    internal sealed class RecordingPower : IMachinePowerController
    {
        public List<(MachinePowerAction Action, TimeSpan Delay)> Scheduled { get; } = [];
        public int Cancels { get; private set; }

        public void Schedule(MachinePowerAction action, TimeSpan delay, string reason) => Scheduled.Add((action, delay));

        public void Cancel() => Cancels++;
    }

    private sealed class FixedProfile(int? idleMinutes) : IProtectionEnforcer
    {
        public IReadOnlyList<BlockedWindowRuleDto> BlockedWindows => [];
        public IReadOnlyList<string> ClearAfterSession => [];
        public ProtectionProfileDto Profile => new(1, false, false, false, false, [], [], [], [], idleMinutes);
        public Task ApplyAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SyncAsync(int serverVersion, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken) => Task.FromResult(SessionEnforcementResult.Accepted("ok", "ok"));
    }

    private sealed class MovableTime(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public sealed class SessionAutostartTests
{
    [Fact]
    public async Task StartsWhatTheClubMarked_AndSkipsWhatIsNotInstalled()
    {
        var present = typeof(SessionAutostartTests).Assembly.Location;
        var launcher = new RecordingLauncher();
        var autostart = new SessionAutostart(
            new Catalog(
            [
                new LauncherEntry("discord", "Discord", "Games", present, "--start-minimized", false, null, null, LaunchOnSessionStart: true),
                new LauncherEntry("dota", "Dota 2", "Games", present, "", false, null, null),
                new LauncherEntry("missing", "Telegram", "Games", null, "", false, null, null, LaunchOnSessionStart: true)
            ]),
            launcher,
            NullLogger<SessionAutostart>.Instance);

        await autostart.StartAsync(CancellationToken.None);

        Assert.Equal([(present, "--start-minimized")], launcher.Launched);
    }

    private sealed class Catalog(IReadOnlyList<LauncherEntry> entries) : ILauncherCatalog
    {
        public IReadOnlyList<LauncherEntry> Entries() => entries;
        public LauncherEntry? Find(string appId) => entries.FirstOrDefault(entry => entry.AppId == appId);
    }

    private sealed class RecordingLauncher : IProcessLauncher
    {
        public List<(string Path, string Arguments)> Launched { get; } = [];

        public Task LaunchAsync(string executablePath, string arguments, CancellationToken cancellationToken)
        {
            Launched.Add((executablePath, arguments));
            return Task.CompletedTask;
        }
    }
}
