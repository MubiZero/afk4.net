using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

public sealed class HttpAssistanceRequestReporter(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceCredentialStore credentialStore) : IAssistanceRequestReporter
{
    public async Task ReportAsync(DateTimeOffset requestedAtUtc, CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{agentOptions.DeviceId:D}/assistance-request")
        {
            Content = JsonContent.Create(new DeviceAssistanceRequest(
                agentOptions.OrganizationId,
                agentOptions.BranchId,
                agentOptions.DeviceId,
                requestedAtUtc))
        };

        // Ключ берётся из хранилища, а не из конфига: после смены в конфиге лежит старый.
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
