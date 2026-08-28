using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public interface ISessionReconciliationReporter
{
    Task<SessionReconciliationResponse> ReportAsync(
        bool isLocked,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);
}

public sealed class SessionReconciliationReporter(
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    ISessionLeaseStore leaseStore,
    IDeviceCredentialStore credentialStore) : ISessionReconciliationReporter
{
    public async Task<SessionReconciliationResponse> ReportAsync(
        bool isLocked,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var agentOptions = options.Value;
        var currentLease = leaseStore.Current;
        var request = new DeviceSessionSnapshotRequest(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            ActiveSessionId: currentLease?.SessionId,
            ActiveLease: currentLease,
            IsLocked: isLocked,
            PendingLocalEventCount: 0,
            ObservedAtUtc: observedAtUtc);
        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        using var message = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/devices/{agentOptions.DeviceId:D}/session-reconciliation")
        {
            Content = JsonContent.Create(request)
        };

        // Ключ берётся из хранилища, а не из конфига: после смены в конфиге лежит старый.
        var credentialSecret = credentialStore.Current;
        if (!string.IsNullOrWhiteSpace(credentialSecret))
        {
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
        }

        using var response = await client.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var reconciliation = await response.Content.ReadFromJsonAsync<SessionReconciliationResponse>(
            cancellationToken: cancellationToken);

        return reconciliation ?? throw new InvalidOperationException("Session reconciliation response was empty.");
    }
}
