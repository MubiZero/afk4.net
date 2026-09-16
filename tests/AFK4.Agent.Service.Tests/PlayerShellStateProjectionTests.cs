using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class PlayerShellStateProjectionTests
{
    private static readonly TimeSpan WorkerStopTimeout = TimeSpan.FromSeconds(20);
    // Минута, а не пятнадцать секунд — столько же, сколько уже ждёт AgentUpdateWorkerTests.
    //
    // Пятнадцати хватает на свободной машине и не хватает на занятой: работник поднимает фоновые
    // задачи и ходит через HTTP-заглушку, и на Windows-раннере один такой кадр уже занимал 22 секунды.
    // Запас ничего не ослабляет: проходящая проверка проходит так же быстро, дольше становится только
    // рассказ о настоящей поломке.
    private static readonly TimeSpan WorkerObservationTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task CreatePlayerShellState_WithConfiguredThresholdAndBranding_ProjectsWarningKindAndBranding()
    {
        // Arrange: session expiring ~60s from now → remainingSeconds ≈ 60 < threshold 120 → LowTime
        var sessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");
        var leaseExpiry = DateTimeOffset.UtcNow.AddSeconds(60);

        var lease = new SessionLeaseDto(
            SessionId: sessionId,
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: "active",
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAtUtc: leaseExpiry,
            SignatureAlgorithm: "none",
            Signature: "test");

        var leaseStore = new InMemorySessionLeaseStore();
        leaseStore.Save(lease);

        var runtimeStateStore = new ActiveRuntimeStateStore(
            sessionId,
            leaseExpiry);

        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var statePublished = new TaskCompletionSource<PlayerShellStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);

        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            ShellWarningThresholdSeconds = 120,
            ClubName = "Club AFK4",
            AccentColor = "#c8ff00"
        });

        using var heartbeatHandler = new AlwaysOkHeartbeatHandler();
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(heartbeatHandler));
        var publisher = new CapturingPlayerShellStatePublisher(statePublished, stopping);

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new NoOpRealtimeClient(),
            leaseStore,
            runtimeStateStore,
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            publisher,
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter(),
            new OfflineGraceState(),
            new InMemoryCommandResultOutbox(),
            new InMemoryDeviceCredentialStore(options.Value.DeviceCredentialSecret),
            new ShellWarningStore(),
            TimeProvider.System);

        // Act
        await worker.StartAsync(stopping.Token);
        var dto = await statePublished.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        // Assert
        Assert.Equal(120, dto.WarningThresholdSeconds);
        Assert.Equal(PlayerShellWarningKinds.LowTime, dto.WarningKind);
        Assert.NotNull(dto.Branding);
        Assert.Equal("Club AFK4", dto.Branding!.ClubName);
        Assert.Equal("#c8ff00", dto.Branding.AccentColor);
    }

    // Открытый счёт: остатка секунд нет вовсе, и оболочка сама предупредить не может. Сервер
    // шлёт warn за минуту до блокировки по долгу — раньше агент отвечал «принято» и выбрасывал
    // команду, так что игрок узнавал о долге по погасшему экрану.
    [Fact]
    public async Task CreatePlayerShellState_ShowsTheServerWarningWhenTheShellHasNothingToWarnAbout()
    {
        var sessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");
        var runtimeStateStore = new ActiveRuntimeStateStore(sessionId, leaseExpiresAtUtc: null);
        var warnings = new ShellWarningStore();
        warnings.Warn(sessionId, PlayerShellWarningKinds.CreditLimit);

        var dto = await PublishStateAsync(
            new InMemorySessionLeaseStore(),
            runtimeStateStore,
            warnings,
            options => options);

        Assert.Equal(PlayerShellWarningKinds.CreditLimit, dto.WarningKind);
    }

    // Предупреждение прошлой сессии не должно висеть на следующем игроке.
    [Fact]
    public async Task CreatePlayerShellState_DropsAWarningLeftFromAnotherSession()
    {
        var sessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");
        var warnings = new ShellWarningStore();
        warnings.Warn(Guid.Parse("11111111-1111-4111-8111-111111111111"), PlayerShellWarningKinds.CreditLimit);

        var dto = await PublishStateAsync(
            new InMemorySessionLeaseStore(),
            new ActiveRuntimeStateStore(sessionId, leaseExpiresAtUtc: null),
            warnings,
            options => options);

        Assert.Equal(PlayerShellWarningKinds.None, dto.WarningKind);
    }

    // Список игр берётся из той же настройки, по которой агент решает, что ему разрешено
    // запускать. Раньше он был захардкожен пустым, и экран игрока не показывал ни одной игры
    // при полностью рабочей настройке и авторизации запуска.
    [Fact]
    public async Task CreatePlayerShellState_ListsConfiguredLauncherApps()
    {
        var presentExecutable = Path.Combine(Path.GetTempPath(), $"afk4-launcher-{Guid.NewGuid():N}.exe");
        await File.WriteAllTextAsync(presentExecutable, "not a real game");

        try
        {
            var dto = await PublishStateAsync(
                new InMemorySessionLeaseStore(),
                new ActiveRuntimeStateStore(Guid.NewGuid(), leaseExpiresAtUtc: null),
                new ShellWarningStore(),
                options =>
                {
                    options.LauncherApps.Add(new AgentLauncherAppOptions
                    {
                        AppId = "cs2",
                        DisplayName = "Counter-Strike 2",
                        Category = "Шутеры",
                        ExecutablePath = presentExecutable
                    });
                    options.LauncherApps.Add(new AgentLauncherAppOptions
                    {
                        AppId = "removed-game",
                        DisplayName = "Снесённая игра",
                        ExecutablePath = Path.Combine(Path.GetTempPath(), $"afk4-missing-{Guid.NewGuid():N}.exe")
                    });
                    options.LauncherApps.Add(new AgentLauncherAppOptions
                    {
                        AppId = "disabled-game",
                        DisplayName = "Выключенная игра",
                        ExecutablePath = presentExecutable,
                        IsEnabled = false
                    });
                    return options;
                });

            Assert.Equal(2, dto.LauncherApps.Count);

            var live = dto.LauncherApps.Single(app => app.AppId == "cs2");
            Assert.Equal("Counter-Strike 2", live.DisplayName);
            Assert.Equal("Шутеры", live.Category);
            Assert.True(live.IsAvailable);

            // Игра, которой на машине больше нет, показывается недоступной, а не прячется:
            // «вчера была, сегодня нет» должно быть видно и игроку, и клубу.
            Assert.False(dto.LauncherApps.Single(app => app.AppId == "removed-game").IsAvailable);
            Assert.DoesNotContain(dto.LauncherApps, app => app.AppId == "disabled-game");
        }
        finally
        {
            File.Delete(presentExecutable);
        }
    }

    private static async Task<PlayerShellStateDto> PublishStateAsync(
        ISessionLeaseStore leaseStore,
        IAgentRuntimeStateStore runtimeStateStore,
        IShellWarningStore warnings,
        Func<AgentOptions, AgentOptions> configure)
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var statePublished = new TaskCompletionSource<PlayerShellStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = Options.Create(configure(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001"
        }));

        using var heartbeatHandler = new AlwaysOkHeartbeatHandler();
        var worker = new Worker(
            NullLogger<Worker>.Instance,
            new TestHttpClientFactory(new HttpClient(heartbeatHandler)),
            options,
            new NoOpRealtimeClient(),
            leaseStore,
            runtimeStateStore,
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new CapturingPlayerShellStatePublisher(statePublished, stopping),
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter(),
            new OfflineGraceState(),
            new InMemoryCommandResultOutbox(),
            new InMemoryDeviceCredentialStore(options.Value.DeviceCredentialSecret),
            warnings,
            TimeProvider.System);

        await worker.StartAsync(stopping.Token);
        var dto = await statePublished.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);
        return dto;
    }

    // Оформление меняют в панели, и на игровой ПК оно приезжает сердцебиением. Конфиг машины
    // остаётся запасным вариантом для самого первого запуска — до первого ответа сервера.
    [Fact]
    public async Task CreatePlayerShellState_PrefersBrandingFromHeartbeatOverTheConfiguredOne()
    {
        var sessionId = Guid.Parse("992624cf-77d1-413b-8e51-6f88872183eb");
        var leaseExpiry = DateTimeOffset.UtcNow.AddSeconds(600);
        var leaseStore = new InMemorySessionLeaseStore();
        leaseStore.Save(new SessionLeaseDto(
            SessionId: sessionId,
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: "active",
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.UtcNow.AddHours(-1),
            ExpiresAtUtc: leaseExpiry,
            SignatureAlgorithm: "none",
            Signature: "test"));

        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var statePublished = new TaskCompletionSource<PlayerShellStateDto>(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            ClubName = "Из конфига",
            AccentColor = "#000000",
        });

        using var heartbeatHandler = new AlwaysOkHeartbeatHandler(
            new ShellBrandingDto("Клуб «Орион»", "https://api.afk4.net/branding/presets/bolt.svg", "#C8FF00"));
        var worker = new Worker(
            NullLogger<Worker>.Instance,
            new TestHttpClientFactory(new HttpClient(heartbeatHandler)),
            options,
            new NoOpRealtimeClient(),
            leaseStore,
            new ActiveRuntimeStateStore(sessionId, leaseExpiry),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            // Первый кадр оболочка получает раньше, чем сервер успевает ответить, — ждём тот,
            // в котором оформление уже пришло сердцебиением.
            new WaitForBrandingPublisher(statePublished, stopping),
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter(),
            new OfflineGraceState(),
            new InMemoryCommandResultOutbox(),
            new InMemoryDeviceCredentialStore(options.Value.DeviceCredentialSecret),
            new ShellWarningStore(),
            TimeProvider.System);

        await worker.StartAsync(stopping.Token);
        var dto = await statePublished.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(dto.Branding);
        Assert.Equal("Клуб «Орион»", dto.Branding!.ClubName);
        Assert.Equal("https://api.afk4.net/branding/presets/bolt.svg", dto.Branding.LogoUrl);
        Assert.Equal("#C8FF00", dto.Branding.AccentColor);
    }

    // --- test doubles (private, scoped to this test class) ---

    private sealed class WaitForBrandingPublisher(
        TaskCompletionSource<PlayerShellStateDto> captured,
        CancellationTokenSource stopping) : IPlayerShellStatePublisher
    {
        public Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken)
        {
            if (state.Branding?.LogoUrl is not null)
            {
                captured.TrySetResult(state);
                stopping.Cancel();
            }

            return Task.CompletedTask;
        }
    }

    private sealed class CapturingPlayerShellStatePublisher(
        TaskCompletionSource<PlayerShellStateDto> captured,
        CancellationTokenSource stopping) : IPlayerShellStatePublisher
    {
        public Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken)
        {
            captured.TrySetResult(state);
            stopping.Cancel();
            return Task.CompletedTask;
        }
    }

    private sealed class AlwaysOkHeartbeatHandler(ShellBrandingDto? branding = null) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new DeviceHeartbeatResponse(
                ServerTimeUtc: DateTimeOffset.UtcNow,
                HeartbeatIntervalSeconds: 10,
                Commands: [],
                Branding: branding);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }

    private sealed class ActiveRuntimeStateStore(Guid sessionId, DateTimeOffset? leaseExpiresAtUtc) : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = new(
            State: PlayerShellStateNames.Active,
            IsLocked: false,
            ActiveSessionId: sessionId,
            LeaseExpiresAtUtc: leaseExpiresAtUtc,
            UpdatedAtUtc: DateTimeOffset.UtcNow);

        public void Save(AgentRuntimeState state) => Current = state;

        public void MarkLocked(DateTimeOffset observedAtUtc) =>
            Current = AgentRuntimeState.Locked(observedAtUtc);

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc) =>
            Current = AgentRuntimeState.Active(lease, observedAtUtc);
    }

    private sealed class NoOpRealtimeClient : IDeviceRealtimeClient
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class NoOpGraceModeMonitor : IGraceModeMonitor
    {
        public Task EnforceAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class NoOpPlayerShellProcessSupervisor : IPlayerShellProcessSupervisor
    {
        public Task EnsureRunningAsync(AgentRuntimeState runtimeState, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class NoOpSessionReconciliationReporter : ISessionReconciliationReporter
    {
        public Task<SessionReconciliationResponse> ReportAsync(
            bool isLocked,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new SessionReconciliationResponse(
                Action: "continue",
                Reason: "test",
                SessionId: null,
                Lease: null));
        }
    }

    private sealed class NoOpDeviceCommandHandler(AgentOptions options) : IDeviceCommandHandler
    {
        public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
        {
            return Task.FromResult(new DeviceCommandResultDto(
                options.OrganizationId,
                options.BranchId,
                options.DeviceId,
                command.CommandId,
                "Accepted",
                "test",
                DateTimeOffset.UtcNow));
        }
    }

    private sealed class NoOpInstalledAppReporter : IInstalledAppReporter
    {
        public Task ReportAsync(
            IReadOnlyCollection<InstalledAppSnapshot> apps,
            DateTimeOffset reportedAtUtc,
            CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class InMemoryCommandResultOutbox : ICommandResultOutbox
    {
        public IReadOnlyList<DeviceCommandResultDto> Pending => [];

        public void Enqueue(DeviceCommandResultDto result) { }

        public void Acknowledge(Guid commandId) { }
    }
}
