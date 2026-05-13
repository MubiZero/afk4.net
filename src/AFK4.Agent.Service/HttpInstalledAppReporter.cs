using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class HttpInstalledAppReporter(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : IInstalledAppReporter
{
    public async Task ReportAsync(
        IReadOnlyCollection<InstalledAppSnapshot> apps,
        DateTimeOffset reportedAtUtc,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var report = InstalledAppReportFactory.Create(agentOptions, apps, reportedAtUtc);
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{agentOptions.DeviceId:D}/installed-apps/report")
        {
            Content = JsonContent.Create(report)
        };

        if (!string.IsNullOrWhiteSpace(agentOptions.DeviceCredentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, agentOptions.DeviceCredentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
