using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class InstalledAppInventoryTests
{
    [Fact]
    public async Task StaticInstalledAppInventoryCollector_CollectAsync_ReturnsConfiguredApps()
    {
        var collector = new StaticInstalledAppInventoryCollector(
        [
            new InstalledAppSnapshot(
                DisplayName: "Counter-Strike 2",
                Version: "2.0.0",
                Publisher: "Valve",
                InstallLocation: @"C:\Games\Counter-Strike 2",
                InstalledAtUtc: DateTimeOffset.Parse("2026-05-01T08:30:00Z"))
        ]);

        var apps = await collector.CollectAsync(CancellationToken.None);

        var app = Assert.Single(apps);
        Assert.Equal("Counter-Strike 2", app.DisplayName);
        Assert.Equal("2.0.0", app.Version);
    }

    [Fact]
    public void InstalledAppRegistryEntryMapper_Map_FiltersNamelessEntriesAndParsesInstalledDate()
    {
        var apps = InstalledAppRegistryEntryMapper.Map(
        [
            new InstalledAppRegistryEntry(
                DisplayName: "  Steam  ",
                DisplayVersion: "  2.10  ",
                Publisher: " Valve ",
                InstallLocation: @" C:\Program Files (x86)\Steam ",
                InstallDate: "20260501"),
            new InstalledAppRegistryEntry(
                DisplayName: "",
                DisplayVersion: "1.0",
                Publisher: "Unknown",
                InstallLocation: null,
                InstallDate: null)
        ]);

        var app = Assert.Single(apps);
        Assert.Equal("Steam", app.DisplayName);
        Assert.Equal("2.10", app.Version);
        Assert.Equal("Valve", app.Publisher);
        Assert.Equal(@"C:\Program Files (x86)\Steam", app.InstallLocation);
        Assert.Equal(DateTimeOffset.Parse("2026-05-01T00:00:00+00:00"), app.InstalledAtUtc);
    }


    [Fact]
    public void InstalledAppReportFactory_Create_BuildsReportForConfiguredDevice()
    {
        var options = CreateOptions();
        var apps = new[]
        {
            new InstalledAppSnapshot(
                DisplayName: "Counter-Strike 2",
                Version: "2.0.0",
                Publisher: "Valve",
                InstallLocation: @"C:\Games\Counter-Strike 2",
                InstalledAtUtc: DateTimeOffset.Parse("2026-05-01T08:30:00Z"))
        };

        var report = InstalledAppReportFactory.Create(
            options,
            apps,
            DateTimeOffset.Parse("2026-05-13T10:00:00Z"));

        Assert.Equal(options.OrganizationId, report.OrganizationId);
        Assert.Equal(options.BranchId, report.BranchId);
        Assert.Equal(options.DeviceId, report.DeviceId);
        Assert.Equal(DateTimeOffset.Parse("2026-05-13T10:00:00Z"), report.ReportedAtUtc);
        var app = Assert.Single(report.Apps);
        Assert.Equal("Counter-Strike 2", app.DisplayName);
        Assert.Equal("2.0.0", app.Version);
        Assert.Equal("Valve", app.Publisher);
        Assert.Equal(@"C:\Games\Counter-Strike 2", app.InstallLocation);
    }

    [Fact]
    public async Task HttpInstalledAppReporter_ReportAsync_PostsDeviceAuthenticatedReport()
    {
        var reportAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new CapturingInstalledAppReportHandler(reportAttempted);
        var reporter = new HttpInstalledAppReporter(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(CreateOptions()));

        await reporter.ReportAsync(
            [
                new InstalledAppSnapshot(
                    DisplayName: "Discord",
                    Version: "1.0.9059",
                    Publisher: "Discord Inc.",
                    InstallLocation: null,
                    InstalledAtUtc: null)
            ],
            DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            CancellationToken.None);
        await reportAttempted.Task.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.Equal($"/api/devices/{CreateOptions().DeviceId:D}/installed-apps/report", handler.RequestUri?.PathAndQuery);
        Assert.Equal("device-secret", handler.CredentialSecret);
        Assert.NotNull(handler.Report);
        Assert.Equal(CreateOptions().DeviceId, handler.Report.DeviceId);
        Assert.Equal("Discord", Assert.Single(handler.Report.Apps).DisplayName);
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

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class CapturingInstalledAppReportHandler(TaskCompletionSource reportAttempted) : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        public string? CredentialSecret { get; private set; }

        public InstalledAppReportRequest? Report { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            CredentialSecret = request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single();
            Report = await request.Content!.ReadFromJsonAsync<InstalledAppReportRequest>(cancellationToken);
            reportAttempted.TrySetResult();

            return new HttpResponseMessage(HttpStatusCode.NoContent);
        }
    }
}
