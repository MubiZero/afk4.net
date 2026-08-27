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
    private static readonly TimeSpan WorkerObservationTimeout = TimeSpan.FromSeconds(15);

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

    // --- test doubles (private, scoped to this test class) ---

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

    private sealed class AlwaysOkHeartbeatHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new DeviceHeartbeatResponse(
                ServerTimeUtc: DateTimeOffset.UtcNow,
                HeartbeatIntervalSeconds: 10,
                Commands: []);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }

    private sealed class ActiveRuntimeStateStore(Guid sessionId, DateTimeOffset leaseExpiry) : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = new(
            State: PlayerShellStateNames.Active,
            IsLocked: false,
            ActiveSessionId: sessionId,
            LeaseExpiresAtUtc: leaseExpiry,
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
