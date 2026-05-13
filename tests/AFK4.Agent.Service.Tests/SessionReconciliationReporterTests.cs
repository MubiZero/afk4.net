using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class SessionReconciliationReporterTests
{
    [Fact]
    public async Task ReportAsync_PostsCurrentLeaseSnapshotWithDeviceCredential()
    {
        var reportAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingReconciliationHandler(reportAttempted);
        var leaseStore = new InMemorySessionLeaseStore();
        var lease = CreateLease();
        leaseStore.Save(lease);
        var reporter = new SessionReconciliationReporter(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(CreateOptions()),
            leaseStore);

        var response = await reporter.ReportAsync(
            isLocked: false,
            observedAtUtc: DateTimeOffset.Parse("2026-05-13T10:02:00Z"),
            CancellationToken.None);
        await reportAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal("continue", response.Action);
        Assert.Equal($"/api/devices/{CreateOptions().DeviceId:D}/session-reconciliation", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
        Assert.NotNull(handler.Request);
        Assert.Equal(lease.SessionId, handler.Request.ActiveSessionId);
        Assert.Equal(lease.Signature, handler.Request.ActiveLease?.Signature);
        Assert.False(handler.Request.IsLocked);
    }

    private static AgentOptions CreateOptions()
    {
        return new AgentOptions
        {
            PlatformBaseUrl = new Uri("https://platform.example"),
            OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName = "PC-001",
            AgentVersion = "0.1.0",
            ShellVersion = "0.1.0",
            DeviceCredentialSecret = "device-secret"
        };
    }

    private static SessionLeaseDto CreateLease()
    {
        return new SessionLeaseDto(
            SessionId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            State: SessionStateNames.Active,
            Sequence: 1,
            IssuedAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            ExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T10:15:00Z"),
            SignatureAlgorithm: "ECDSA-P256-SHA256",
            Signature: "signed-payload");
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class CapturingReconciliationHandler(TaskCompletionSource reportAttempted) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? CredentialSecret { get; private set; }

        public DeviceSessionSnapshotRequest? Request { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            CredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();
            Request = await request.Content!.ReadFromJsonAsync<DeviceSessionSnapshotRequest>(cancellationToken);
            reportAttempted.TrySetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new SessionReconciliationResponse(
                    Action: "continue",
                    Reason: "local-lease-current",
                    SessionId: Request?.ActiveSessionId,
                    Lease: null))
            };
        }
    }
}
