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

public sealed class WorkerTests
{
    private static readonly TimeSpan WorkerStopTimeout = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan WorkerObservationTimeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task ExecuteAsync_AttemptsHeartbeatWhenRealtimeStartupThrowsNonCancellationException()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var heartbeatAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingHeartbeatHandler(heartbeatAttempted, stopping);
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            DeviceCredentialSecret = "device-secret"
        });

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new ThrowingRealtimeClient(new InvalidOperationException("realtime unavailable")),
            new InMemorySessionLeaseStore(),
            new RecordingRuntimeStateStore(isLocked: true),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new NoOpPlayerShellStatePublisher(),
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter());

        await worker.StartAsync(stopping.Token);
        await heartbeatAttempted.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal($"/api/devices/{options.Value.DeviceId}/heartbeat", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
    }

    [Fact]
    public async Task ExecuteAsync_HeartbeatUsesRuntimeLockState()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var heartbeatAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingHeartbeatHandler(heartbeatAttempted, stopping);
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            DeviceCredentialSecret = "device-secret"
        });

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new NoOpRealtimeClient(),
            new InMemorySessionLeaseStore(),
            new RecordingRuntimeStateStore(isLocked: false),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new NoOpPlayerShellStatePublisher(),
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter());

        await worker.StartAsync(stopping.Token);
        await heartbeatAttempted.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.NotNull(handler.Request);
        Assert.False(handler.Request.IsLocked);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsInstalledAppsBeforeHeartbeatLoop()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var heartbeatAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingHeartbeatHandler(heartbeatAttempted, stopping);
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            DeviceCredentialSecret = "device-secret"
        });
        var reporter = new RecordingInstalledAppReporter();

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new NoOpRealtimeClient(),
            new InMemorySessionLeaseStore(),
            new RecordingRuntimeStateStore(isLocked: true),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new NoOpPlayerShellStatePublisher(),
            new NoOpDeviceCommandHandler(options.Value),
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector(
            [
                new InstalledAppSnapshot(
                    DisplayName: "Discord",
                    Version: "1.0.9059",
                    Publisher: "Discord Inc.",
                    InstallLocation: null,
                    InstalledAtUtc: null)
            ]),
            reporter);

        await worker.StartAsync(stopping.Token);
        await heartbeatAttempted.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        var app = Assert.Single(reporter.LastApps);
        Assert.Equal("Discord", app.DisplayName);
        Assert.NotNull(reporter.LastReportedAtUtc);
    }

    [Fact]
    public async Task ExecuteAsync_ReconcilesSessionBeforeInstalledAppsAndHeartbeatLoop()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var heartbeatAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingHeartbeatHandler(heartbeatAttempted, stopping);
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            DeviceCredentialSecret = "device-secret"
        });
        var calls = new List<string>();

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new NoOpRealtimeClient(),
            new InMemorySessionLeaseStore(),
            new RecordingRuntimeStateStore(isLocked: true),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new NoOpPlayerShellStatePublisher(),
            new NoOpDeviceCommandHandler(options.Value),
            new RecordingSessionReconciliationReporter(calls),
            new StaticInstalledAppInventoryCollector(
            [
                new InstalledAppSnapshot(
                    DisplayName: "Discord",
                    Version: "1.0.9059",
                    Publisher: "Discord Inc.",
                    InstallLocation: null,
                    InstalledAtUtc: null)
            ]),
            new RecordingInstalledAppReporter(calls));

        await worker.StartAsync(stopping.Token);
        await heartbeatAttempted.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal(["reconcile", "apps"], calls);
    }

    [Fact]
    public async Task ExecuteAsync_HandlesHeartbeatCommandsAndReportsResultWithCredential()
    {
        using var stopping = new CancellationTokenSource(WorkerStopTimeout);
        var resultPosted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var options = Options.Create(new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            DeviceCredentialSecret = "device-secret"
        });
        var command = new DeviceCommandDto(
            Guid.Parse("aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa"),
            "lock",
            DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            new Dictionary<string, string>
            {
                ["reason"] = "heartbeat-fallback"
            });
        using var handler = new HeartbeatCommandResultHandler(command, resultPosted, stopping);
        var httpClientFactory = new TestHttpClientFactory(new HttpClient(handler));
        var commandHandler = new RecordingDeviceCommandHandler(options.Value);

        var worker = new Worker(
            NullLogger<Worker>.Instance,
            httpClientFactory,
            options,
            new NoOpRealtimeClient(),
            new InMemorySessionLeaseStore(),
            new RecordingRuntimeStateStore(isLocked: true),
            new NoOpGraceModeMonitor(),
            new NoOpPlayerShellProcessSupervisor(),
            new NoOpPlayerShellStatePublisher(),
            commandHandler,
            new NoOpSessionReconciliationReporter(),
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter());

        await worker.StartAsync(stopping.Token);
        await resultPosted.Task.WaitAsync(WorkerObservationTimeout);
        await worker.StopAsync(CancellationToken.None);

        var handled = Assert.Single(commandHandler.HandledCommands);
        Assert.Equal(command.CommandId, handled.CommandId);
        Assert.Equal($"/api/devices/{options.Value.DeviceId:D}/commands/{command.CommandId:D}/result", handler.ResultRequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.ResultCredentialSecret);
        Assert.NotNull(handler.ResultBody);
        Assert.Equal(command.CommandId, handler.ResultBody.CommandId);
        Assert.Equal("Accepted", handler.ResultBody.Status);
    }

    private sealed class ThrowingRealtimeClient(Exception exception) : IDeviceRealtimeClient
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.FromException(exception);
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class NoOpRealtimeClient : IDeviceRealtimeClient
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
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

    private sealed class NoOpGraceModeMonitor : IGraceModeMonitor
    {
        public Task EnforceAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpPlayerShellProcessSupervisor : IPlayerShellProcessSupervisor
    {
        public Task EnsureRunningAsync(AgentRuntimeState runtimeState, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NoOpPlayerShellStatePublisher : IPlayerShellStatePublisher
    {
        public Task PublishAsync(PlayerShellStateDto state, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingRuntimeStateStore(bool isLocked) : IAgentRuntimeStateStore
    {
        public AgentRuntimeState Current { get; private set; } = new(
            State: isLocked ? PlayerShellStateNames.Locked : PlayerShellStateNames.Active,
            IsLocked: isLocked,
            ActiveSessionId: null,
            LeaseExpiresAtUtc: null,
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"));

        public void Save(AgentRuntimeState state)
        {
            Current = state;
        }

        public void MarkLocked(DateTimeOffset observedAtUtc)
        {
            Current = AgentRuntimeState.Locked(observedAtUtc);
        }

        public void MarkActive(SessionLeaseDto lease, DateTimeOffset observedAtUtc)
        {
            Current = AgentRuntimeState.Active(lease, observedAtUtc);
        }
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
            return Task.FromResult(CreateResult(options, command, "Accepted"));
        }
    }

    private sealed class RecordingDeviceCommandHandler(AgentOptions options) : IDeviceCommandHandler
    {
        public List<DeviceCommandDto> HandledCommands { get; } = [];

        public Task<DeviceCommandResultDto> HandleAsync(DeviceCommandDto command, CancellationToken cancellationToken)
        {
            HandledCommands.Add(command);
            return Task.FromResult(CreateResult(options, command, "Accepted"));
        }
    }

    private static DeviceCommandResultDto CreateResult(
        AgentOptions options,
        DeviceCommandDto command,
        string status)
    {
        return new DeviceCommandResultDto(
            options.OrganizationId,
            options.BranchId,
            options.DeviceId,
            command.CommandId,
            status,
            "test result",
            DateTimeOffset.Parse("2026-05-13T10:01:00Z"));
    }

    private sealed class RecordingSessionReconciliationReporter(List<string> calls) : ISessionReconciliationReporter
    {
        public Task<SessionReconciliationResponse> ReportAsync(
            bool isLocked,
            DateTimeOffset observedAtUtc,
            CancellationToken cancellationToken)
        {
            calls.Add("reconcile");

            return Task.FromResult(new SessionReconciliationResponse(
                Action: "continue",
                Reason: "test",
                SessionId: null,
                Lease: null));
        }
    }

    private sealed class RecordingInstalledAppReporter : IInstalledAppReporter
    {
        private readonly List<string>? calls;

        public RecordingInstalledAppReporter()
        {
        }

        public RecordingInstalledAppReporter(List<string> calls)
        {
            this.calls = calls;
        }

        public IReadOnlyCollection<InstalledAppSnapshot> LastApps { get; private set; } = [];

        public DateTimeOffset? LastReportedAtUtc { get; private set; }

        public Task ReportAsync(
            IReadOnlyCollection<InstalledAppSnapshot> apps,
            DateTimeOffset reportedAtUtc,
            CancellationToken cancellationToken)
        {
            calls?.Add("apps");
            LastApps = apps;
            LastReportedAtUtc = reportedAtUtc;
            return Task.CompletedTask;
        }
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class CapturingHeartbeatHandler(
        TaskCompletionSource heartbeatAttempted,
        CancellationTokenSource stopping) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? CredentialSecret { get; private set; }

        public DeviceHeartbeatRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            CredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();
            Request = await request.Content!.ReadFromJsonAsync<DeviceHeartbeatRequest>(cancellationToken: cancellationToken);
            heartbeatAttempted.TrySetResult();
            stopping.Cancel();

            var response = new DeviceHeartbeatResponse(
                ServerTimeUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
                HeartbeatIntervalSeconds: 10,
                Commands: []);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            };
        }
    }

    private sealed class HeartbeatCommandResultHandler(
        DeviceCommandDto command,
        TaskCompletionSource resultPosted,
        CancellationTokenSource stopping) : HttpMessageHandler
    {
        public Uri? ResultRequestUri { get; private set; }

        public string? ResultCredentialSecret { get; private set; }

        public DeviceCommandResultDto? ResultBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            if (request.RequestUri?.PathAndQuery.EndsWith("/heartbeat", StringComparison.Ordinal) == true)
            {
                var response = new DeviceHeartbeatResponse(
                    ServerTimeUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
                    HeartbeatIntervalSeconds: 10,
                    Commands: [command]);

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(response)
                };
            }

            ResultRequestUri = request.RequestUri;
            ResultCredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();
            ResultBody = await request.Content!.ReadFromJsonAsync<DeviceCommandResultDto>(cancellationToken: cancellationToken);
            resultPosted.TrySetResult();
            stopping.Cancel();

            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
