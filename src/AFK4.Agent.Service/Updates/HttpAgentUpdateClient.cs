using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class HttpAgentUpdateClient(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options) : IAgentUpdateClient
{
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(
        IReadOnlyList<DeviceComponentVersionDto> installedComponents,
        DateTimeOffset checkedAtUtc,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var request = new DeviceUpdateCheckRequest(
            agentOptions.OrganizationId,
            agentOptions.BranchId,
            agentOptions.DeviceId,
            agentOptions.UpdateChannel,
            checkedAtUtc,
            installedComponents);
        var response = await SendAsync<DeviceUpdateCheckRequest, DeviceUpdateCheckResponse>(
            $"/api/devices/{agentOptions.DeviceId:D}/updates/check",
            request,
            cancellationToken);

        return new UpdateCheckResult(response.ServerTimeUtc, response.Updates);
    }

    public async Task<DeviceUpdateStatusResultDto> ReportStatusAsync(
        Guid updateRolloutId,
        Guid updatePackageId,
        string component,
        string installedVersion,
        string targetVersion,
        string status,
        string message,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var request = new DeviceUpdateStatusReportRequest(
            agentOptions.OrganizationId,
            agentOptions.BranchId,
            agentOptions.DeviceId,
            updateRolloutId,
            updatePackageId,
            component,
            installedVersion,
            targetVersion,
            status,
            message,
            observedAtUtc);

        return await SendAsync<DeviceUpdateStatusReportRequest, DeviceUpdateStatusResultDto>(
            $"/api/devices/{agentOptions.DeviceId:D}/updates/status",
            request,
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest request,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        using var message = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request)
        };

        if (!string.IsNullOrWhiteSpace(agentOptions.DeviceCredentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, agentOptions.DeviceCredentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: cancellationToken);

        return body ?? throw new InvalidOperationException("Update response was empty.");
    }
}
