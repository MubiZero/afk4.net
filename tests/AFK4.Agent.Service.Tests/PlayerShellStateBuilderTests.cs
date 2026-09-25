using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Сборщик состояния оболочки — то, что игрок видит на экране. Раньше оно собиралось внутри
/// работника и проверялось только прогоном всего цикла с HTTP-заглушкой: минута ожидания на
/// кадр и ни одной проверки того, что агент говорит правду о связи.
/// </summary>
public sealed class PlayerShellStateBuilderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-24T20:00:00Z");
    private static readonly Guid SessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");

    [Fact]
    public void Locked_WithFreshContact_IsLockedAndShowsTheSeatingCode()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-5), intervalSeconds: 10);
        fixture.Heartbeat.Record("418207", Now.AddMinutes(1), branding: null, intervalSeconds: 10);

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Locked, state.State);
        Assert.True(state.IsOnline);
        Assert.Equal("418207", state.SeatingCode);
        Assert.Equal(Now.AddMinutes(1), state.SeatingCodeExpiresAtUtc);
        Assert.Equal(Now.AddSeconds(-5), state.LastContactUtc);
        Assert.Equal(Now, state.ObservedAtUtc);
    }

    [Fact]
    public void Locked_WithoutContactForTwoIntervals_IsOfflineAndHidesTheSeatingCode()
    {
        // Показать старый код значит позвать человека к машине, которую сервер ему не отдаст.
        var fixture = new Fixture();
        fixture.Heartbeat.Record("418207", Now.AddMinutes(1), branding: null, intervalSeconds: 10);
        fixture.Contact(Now.AddSeconds(-40), intervalSeconds: 10);

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Offline, state.State);
        Assert.False(state.IsOnline);
        Assert.Null(state.SeatingCode);
        Assert.Equal(PlayerShellWarningKinds.Connectivity, state.WarningKind);
    }

    [Fact]
    public void Locked_BeforeAnyContactSinceStart_IsOffline()
    {
        var state = new Fixture().Build();

        Assert.Equal(PlayerShellStateNames.Offline, state.State);
        Assert.False(state.IsOnline);
        Assert.Null(state.LastContactUtc);
    }

    [Fact]
    public void Locked_WithAnExpiredSeatingCode_HidesIt()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 10);
        fixture.Heartbeat.Record("418207", Now.AddSeconds(-1), branding: null, intervalSeconds: 10);

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Locked, state.State);
        Assert.Null(state.SeatingCode);
    }

    [Fact]
    public void Active_WithMoreThanAMinuteLeft_IsActive()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 3);
        fixture.StartSession(Now.AddMinutes(30));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Active, state.State);
        Assert.Equal(SessionId, state.SessionId);
        Assert.Equal(1800, state.RemainingSeconds);
        Assert.Null(state.SeatingCode);
        Assert.Equal(PlayerShellWarningKinds.None, state.WarningKind);
    }

    [Fact]
    public void Active_InTheLastMinute_IsEnding()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 3);
        fixture.StartSession(Now.AddSeconds(45));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Ending, state.State);
        Assert.Equal(PlayerShellWarningKinds.LowTime, state.WarningKind);
    }

    [Fact]
    public void Active_WhenTheConnectionDrops_StaysActiveButSaysSo()
    {
        // Оплаченная сессия идёт по аренде, экран лишь честно говорит, что связи нет.
        var fixture = new Fixture();
        fixture.Contact(Now.AddMinutes(-2), intervalSeconds: 10);
        fixture.StartSession(Now.AddMinutes(30));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Active, state.State);
        Assert.False(state.IsOnline);
        Assert.Equal(PlayerShellWarningKinds.Connectivity, state.WarningKind);
    }

    [Fact]
    public void Grace_IsReportedAsConnectivity_NotAsCreditLimit()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddMinutes(-3), intervalSeconds: 10);
        fixture.RuntimeState.Save(AgentRuntimeState.Grace(SessionId, Now.AddMinutes(-1), Now));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Grace, state.State);
        Assert.True(state.IsGraceMode);
        Assert.Equal(PlayerShellWarningKinds.Connectivity, state.WarningKind);
    }

    [Fact]
    public void ServerWarning_ShowsWhenTheMachineItselfHasNothingToSay()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 3);
        fixture.StartSession(Now.AddMinutes(30));
        fixture.Warnings.Warn(SessionId, PlayerShellWarningKinds.CreditLimit);

        var state = fixture.Build();

        Assert.Equal(PlayerShellWarningKinds.CreditLimit, state.WarningKind);
    }

    [Fact]
    public void Active_BelowTheClubsWarningThreshold_WarnsAboutTime()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 3);
        fixture.StartSession(Now.AddSeconds(200));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Active, state.State);
        Assert.Equal(300, state.WarningThresholdSeconds);
        Assert.Equal(PlayerShellWarningKinds.LowTime, state.WarningKind);
    }

    // Предупреждение прошлой сессии не должно висеть на следующем игроке.
    [Fact]
    public void ServerWarning_FromAnotherSession_IsDropped()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 3);
        fixture.StartSession(Now.AddMinutes(30));
        fixture.Warnings.Warn(Guid.Parse("11111111-1111-4111-8111-111111111111"), PlayerShellWarningKinds.CreditLimit);

        var state = fixture.Build();

        Assert.Equal(PlayerShellWarningKinds.None, state.WarningKind);
        Assert.Null(fixture.Warnings.Current);
    }

    // Оформление меняют в панели, и на игровой ПК оно приезжает сердцебиением. Конфиг машины
    // остаётся запасным вариантом для самого первого запуска — до первого ответа сервера.
    [Fact]
    public void Branding_FromTheHeartbeat_BeatsTheConfiguredOne()
    {
        var fixture = new Fixture(clubName: "Из конфига");
        Assert.Equal("Из конфига", fixture.Build().Branding!.ClubName);

        fixture.Heartbeat.Record(null, null, new ShellBrandingDto("Клуб «Орион»", "https://api.afk4.net/branding/presets/bolt.svg", "#C8FF00"), 10);

        var branding = fixture.Build().Branding!;
        Assert.Equal("Клуб «Орион»", branding.ClubName);
        Assert.Equal("#C8FF00", branding.AccentColor);
    }

    // Список игр берётся из той же настройки, по которой агент решает, что ему разрешено
    // запускать. Игра, которой на машине больше нет, показывается недоступной, а не прячется:
    // «вчера была, сегодня нет» должно быть видно и игроку, и клубу.
    [Fact]
    public void LauncherApps_FollowTheSameListTheAgentLaunchesFrom()
    {
        var presentExecutable = Path.Combine(Path.GetTempPath(), $"afk4-launcher-{Guid.NewGuid():N}.exe");
        File.WriteAllText(presentExecutable, "not a real game");
        try
        {
            var fixture = new Fixture();
            fixture.Options.LauncherApps.Add(new AgentLauncherAppOptions
            {
                AppId = "cs2",
                DisplayName = "Counter-Strike 2",
                Category = "Шутеры",
                ExecutablePath = presentExecutable
            });
            fixture.Options.LauncherApps.Add(new AgentLauncherAppOptions
            {
                AppId = "removed-game",
                DisplayName = "Снесённая игра",
                ExecutablePath = Path.Combine(Path.GetTempPath(), $"afk4-missing-{Guid.NewGuid():N}.exe")
            });
            fixture.Options.LauncherApps.Add(new AgentLauncherAppOptions
            {
                AppId = "disabled-game",
                DisplayName = "Выключенная игра",
                ExecutablePath = presentExecutable,
                IsEnabled = false
            });

            var apps = fixture.Build().LauncherApps;

            Assert.Equal(2, apps.Count);
            var live = apps.Single(app => app.AppId == "cs2");
            Assert.Equal("Шутеры", live.Category);
            Assert.True(live.IsAvailable);
            Assert.False(apps.Single(app => app.AppId == "removed-game").IsAvailable);
            Assert.DoesNotContain(apps, app => app.AppId == "disabled-game");
        }
        finally
        {
            File.Delete(presentExecutable);
        }
    }

    [Fact]
    public void ApiBaseUrl_ComesFromTheAgentsOwnSettings()
    {
        var state = new Fixture().Build();

        Assert.Equal("https://api.example.test/", state.ApiBaseUrl);
    }

    [Fact]
    public void OnlineWindow_FollowsTheIntervalTheServerNamed()
    {
        // Сервер ускоряет сердцебиение до 3 с, пока команды в пути, и замедляет до минуты.
        // Окно «на связи» идёт за ним, а не за жёсткой цифрой.
        Assert.True(ShellConnectivity.IsOnline(Now.AddSeconds(-100), intervalSeconds: 60, Now));
        Assert.False(ShellConnectivity.IsOnline(Now.AddSeconds(-20), intervalSeconds: 3, Now));
        Assert.True(ShellConnectivity.IsOnline(Now.AddSeconds(-14), intervalSeconds: 3, Now));
        Assert.False(ShellConnectivity.IsOnline(null, intervalSeconds: 10, Now));
    }

    [Fact]
    public void SeatOwnerAndFeatures_FromTheHeartbeat_ReachTheScreen()
    {
        var fixture = new Fixture();
        fixture.Contact(Now.AddSeconds(-2), intervalSeconds: 10);
        var owner = Guid.NewGuid();
        fixture.Heartbeat.RecordPlace(
            new AFK4.Shared.Contracts.Devices.DeviceSeatDto("ПК 07", "Общий зал"),
            new AFK4.Shared.Contracts.Devices.DeviceSessionOwnerDto(AFK4.Shared.Contracts.Devices.DeviceSessionOwnerKindNames.Player, owner),
            ["player_shop"]);

        var state = fixture.Build();

        Assert.Equal("ПК 07", state.SeatLabel);
        Assert.Equal("Общий зал", state.ZoneName);
        Assert.Equal(AFK4.Shared.Contracts.Devices.DeviceSessionOwnerKindNames.Player, state.SessionOwnerKind);
        Assert.Equal(owner, state.SessionOwnerPlayerAccountId);
        Assert.Equal(["player_shop"], state.Features);
    }

    [Fact]
    public void Maintenance_StaysMaintenanceWithoutConnection_AndShowsNoSeatingCode()
    {
        // «Нет связи» позвало бы разбираться с сетью; клуб закрыл машину сам, и код к ней звать не должен.
        var fixture = new Fixture();
        fixture.Heartbeat.Record("418207", Now.AddMinutes(1), branding: null, intervalSeconds: 10);
        fixture.RuntimeState.Save(AgentRuntimeState.Maintenance(Now));

        var state = fixture.Build();

        Assert.Equal(PlayerShellStateNames.Maintenance, state.State);
        Assert.Null(state.SeatingCode);
    }

    /// <summary>Полоса обслуживания пишет, кто и когда: оболочка берёт это из состояния.</summary>
    [Fact]
    public void Maintenance_CarriesWhoTurnedItOnAndSince_AndOnlyThere()
    {
        var fixture = new Fixture();
        fixture.Heartbeat.RecordMaintenance(Now.AddMinutes(-30), "Шерзод");
        fixture.RuntimeState.Save(AgentRuntimeState.Maintenance(Now));

        var maintenance = fixture.Build();

        Assert.Equal(Now.AddMinutes(-30), maintenance.MaintenanceSinceUtc);
        Assert.Equal("Шерзод", maintenance.MaintenanceByName);

        fixture.RuntimeState.Save(AgentRuntimeState.Locked(Now));
        var locked = fixture.Build();

        Assert.Null(locked.MaintenanceSinceUtc);
        Assert.Null(locked.MaintenanceByName);
    }

    /// <summary>Правила закрытия окон едут хосту; в обслуживании технику нужны и командная строка, и реестр.</summary>
    [Fact]
    public void BlockedWindows_TravelToTheHost_ExceptUnderMaintenance()
    {
        var rules = new List<AFK4.Shared.Contracts.Devices.BlockedWindowRuleDto> { new("Командная строка", null) };
        var fixture = new Fixture(protection: new StubProtection(rules));

        Assert.Equal(rules, fixture.Build().BlockedWindows);

        fixture.RuntimeState.Save(AgentRuntimeState.Maintenance(Now));
        Assert.Empty(fixture.Build().BlockedWindows!);
    }

    private sealed class StubProtection(IReadOnlyList<AFK4.Shared.Contracts.Devices.BlockedWindowRuleDto> rules)
        : AFK4.Agent.Service.Protection.IProtectionEnforcer
    {
        public IReadOnlyList<AFK4.Shared.Contracts.Devices.BlockedWindowRuleDto> BlockedWindows => rules;

        public IReadOnlyList<string> ClearAfterSession => [];

        public Task ApplyAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ReleaseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SyncAsync(int serverVersion, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SessionEnforcementResult> RefreshAsync(CancellationToken cancellationToken) =>
            Task.FromResult(SessionEnforcementResult.Accepted("ok", "protection-applied"));
    }

    private sealed class Fixture(string? clubName = null, AFK4.Agent.Service.Protection.IProtectionEnforcer? protection = null)
    {
        public AgentOptions Options { get; } = new()
        {
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            PlatformBaseUrl = new Uri("https://api.example.test/"),
            ShellWarningThresholdSeconds = 300,
            ClubName = clubName
        };

        public InMemorySessionLeaseStore Leases { get; } = new();

        public MemoryRuntimeStateStore RuntimeState { get; } = new(AgentRuntimeState.Locked(Now));

        public OfflineGraceState Grace { get; } = new();

        public ShellHeartbeatSnapshot Heartbeat { get; } = new();

        public ShellWarningStore Warnings { get; } = new();

        public void Contact(DateTimeOffset at, int intervalSeconds)
        {
            Grace.RecordSuccessfulContact(at, effectiveGraceMinutes: 15);
            Heartbeat.Record(Heartbeat.SeatingCode, Heartbeat.SeatingCodeExpiresAtUtc, branding: null, intervalSeconds);
        }

        public void StartSession(DateTimeOffset expiresAt)
        {
            var lease = new SessionLeaseDto(
                SessionId,
                OrganizationId: Guid.NewGuid(),
                BranchId: Guid.NewGuid(),
                SeatId: Guid.NewGuid(),
                DeviceId: Guid.NewGuid(),
                State: "active",
                Sequence: 1,
                IssuedAtUtc: Now.AddHours(-1),
                ExpiresAtUtc: expiresAt,
                SignatureAlgorithm: "none",
                Signature: "test");
            Leases.Save(lease);
            RuntimeState.MarkActive(lease, Now);
        }

        public PlayerShellStateDto Build() => new PlayerShellStateBuilder(
            Microsoft.Extensions.Options.Options.Create(Options),
            Leases,
            RuntimeState,
            Grace,
            Heartbeat,
            Warnings,
            new FixedTimeProvider(Now),
            protection).Build();
    }

    internal sealed class MemoryRuntimeStateStore(AgentRuntimeState initial) : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = initial;

        public void Save(AgentRuntimeState state) => Current = state;

        public void MarkLocked(DateTimeOffset observedAtUtc) => Current = AgentRuntimeState.Locked(observedAtUtc);

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc) =>
            Current = AgentRuntimeState.Active(lease, observedAtUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
