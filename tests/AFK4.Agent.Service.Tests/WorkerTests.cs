using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class WorkerTests
{
    [Fact]
    public async Task ExecuteAsync_AttemptsHeartbeatWhenRealtimeStartupThrowsNonCancellationException()
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
            new StaticInstalledAppInventoryCollector([]),
            new NoOpInstalledAppReporter());

        await worker.StartAsync(stopping.Token);
        await heartbeatAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        Assert.Equal($"/api/devices/{options.Value.DeviceId}/heartbeat", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
    }

    [Fact]
    public async Task ExecuteAsync_ReportsInstalledAppsBeforeHeartbeatLoop()
    {
        using var stopping = new CancellationTokenSource(TimeSpan.FromSeconds(5));
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
        await heartbeatAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await worker.StopAsync(CancellationToken.None);

        var app = Assert.Single(reporter.LastApps);
        Assert.Equal("Discord", app.DisplayName);
        Assert.NotNull(reporter.LastReportedAtUtc);
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

    private sealed class RecordingInstalledAppReporter : IInstalledAppReporter
    {
        public IReadOnlyCollection<InstalledAppSnapshot> LastApps { get; private set; } = [];

        public DateTimeOffset? LastReportedAtUtc { get; private set; }

        public Task ReportAsync(
            IReadOnlyCollection<InstalledAppSnapshot> apps,
            DateTimeOffset reportedAtUtc,
            CancellationToken cancellationToken)
        {
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

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            CredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();
            heartbeatAttempted.TrySetResult();
            stopping.Cancel();

            var response = new DeviceHeartbeatResponse(
                ServerTimeUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
                HeartbeatIntervalSeconds: 10,
                Commands: []);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(response)
            });
        }
    }
}
