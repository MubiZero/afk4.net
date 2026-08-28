using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class HttpAgentUpdateClientTests
{
    [Fact]
    public async Task CheckForUpdatesAsync_PostsInstalledComponentsWithDeviceCredential()
    {
        var requestCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingUpdateHandler(requestCaptured);
        var client = new HttpAgentUpdateClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(CreateOptions()),
            new InMemoryDeviceCredentialStore());

        var result = await client.CheckForUpdatesAsync(
            [new DeviceComponentVersionDto(UpdateComponentNames.AgentService, "1.2.2")],
            DateTimeOffset.Parse("2026-05-14T15:00:00Z"),
            CancellationToken.None);
        await requestCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.True(result.HasUpdates);
        Assert.Equal($"/api/devices/{CreateOptions().DeviceId:D}/updates/check", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
        Assert.NotNull(handler.CheckRequest);
        Assert.Equal(CreateOptions().DeviceId, handler.CheckRequest.DeviceId);
        Assert.Equal(UpdateChannelNames.Beta, handler.CheckRequest.Channel);
        Assert.Equal("1.2.2", handler.CheckRequest.InstalledComponents[0].Version);
        Assert.Equal("1.2.3", result.Updates[0].Version);
    }

    [Fact]
    public async Task CheckForUpdatesAsync_EmptyResponseReturnsNoUpdates()
    {
        using var handler = new EmptyUpdateCheckHandler();
        var client = new HttpAgentUpdateClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(CreateOptions()),
            new InMemoryDeviceCredentialStore());

        var result = await client.CheckForUpdatesAsync(
            [],
            DateTimeOffset.Parse("2026-05-14T15:00:00Z"),
            CancellationToken.None);

        Assert.False(result.HasUpdates);
        Assert.Empty(result.Updates);
    }

    [Fact]
    public async Task ReportStatusAsync_PostsUpdateStatusWithDeviceCredential()
    {
        var requestCaptured = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingUpdateHandler(requestCaptured);
        var client = new HttpAgentUpdateClient(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(CreateOptions()),
            new InMemoryDeviceCredentialStore());
        var rolloutId = Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb");
        var packageId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

        var result = await client.ReportStatusAsync(
            rolloutId,
            packageId,
            UpdateComponentNames.AgentService,
            installedVersion: "1.2.2",
            targetVersion: "1.2.3",
            UpdateStatusNames.Installing,
            message: "install started",
            observedAtUtc: DateTimeOffset.Parse("2026-05-14T15:01:00Z"),
            CancellationToken.None);
        await requestCaptured.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal(UpdateStatusNames.Installing, result.Status);
        Assert.Equal($"/api/devices/{CreateOptions().DeviceId:D}/updates/status", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
        Assert.NotNull(handler.StatusRequest);
        Assert.Equal(rolloutId, handler.StatusRequest.UpdateRolloutId);
        Assert.Equal(packageId, handler.StatusRequest.UpdatePackageId);
        Assert.Equal("install started", handler.StatusRequest.Message);
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
            AgentVersion = "1.2.2",
            ShellVersion = "1.2.2",
            DeviceCredentialSecret = "device-secret",
            UpdateChannel = UpdateChannelNames.Beta
        };
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class CapturingUpdateHandler(TaskCompletionSource requestCaptured) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? CredentialSecret { get; private set; }

        public DeviceUpdateCheckRequest? CheckRequest { get; private set; }

        public DeviceUpdateStatusReportRequest? StatusRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            CredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();

            if (request.RequestUri?.AbsolutePath.EndsWith("/updates/check", StringComparison.Ordinal) == true)
            {
                CheckRequest = await request.Content!.ReadFromJsonAsync<DeviceUpdateCheckRequest>(cancellationToken);
                requestCaptured.TrySetResult();

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new DeviceUpdateCheckResponse(
                        ServerTimeUtc: DateTimeOffset.Parse("2026-05-14T15:00:01Z"),
                        Updates:
                        [
                            new ComponentUpdateInstructionDto(
                                UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
                                UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
                                Component: UpdateComponentNames.AgentService,
                                Version: "1.2.3",
                                Channel: UpdateChannelNames.Beta,
                                ArtifactUri: "https://updates.afk4.test/agent/1.2.3/agent.msi",
                                Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
                                Signature: "base64-signature",
                                SignatureAlgorithm: "ed25519",
                                SizeBytes: 42_000_000,
                                ReleaseNotes: "Agent update.")
                        ]))
                };
            }

            StatusRequest = await request.Content!.ReadFromJsonAsync<DeviceUpdateStatusReportRequest>(cancellationToken);
            requestCaptured.TrySetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DeviceUpdateStatusResultDto(
                    StatusRequest!.DeviceId,
                    StatusRequest.UpdateRolloutId,
                    StatusRequest.UpdatePackageId,
                    StatusRequest.Component,
                    StatusRequest.Status,
                    StatusRequest.Message,
                    StatusRequest.ObservedAtUtc))
            };
        }
    }

    private sealed class EmptyUpdateCheckHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new DeviceUpdateCheckResponse(
                    ServerTimeUtc: DateTimeOffset.Parse("2026-05-14T15:00:01Z"),
                    Updates: []))
            });
        }
    }
}
