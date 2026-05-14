using System.Net.Http.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Shared.Contracts.Devices;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service;

public sealed class Worker(
    ILogger<Worker> logger,
    IHttpClientFactory httpClientFactory,
    IOptions<AgentOptions> options,
    IDeviceRealtimeClient realtimeClient,
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IGraceModeMonitor graceModeMonitor,
    IDeviceCommandHandler commandHandler,
    ISessionReconciliationReporter sessionReconciliationReporter,
    IInstalledAppInventoryCollector installedAppInventoryCollector,
    IInstalledAppReporter installedAppReporter) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        try
        {
            await realtimeClient.StartAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Realtime device channel failed to start. Continuing with HTTP heartbeats.");
        }

        var client = httpClientFactory.CreateClient("platform");
        client.BaseAddress = agentOptions.PlatformBaseUrl;

        await TryEnforceGraceModeAsync(stoppingToken);
        await TryReconcileSessionAsync(stoppingToken);
        await TryReportInstalledAppsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TryEnforceGraceModeAsync(stoppingToken);
            var runtimeState = runtimeStateStore.Current;
            var request = HeartbeatPayloadFactory.Create(
                agentOptions,
                runtimeState.IsLocked,
                DateTimeOffset.UtcNow,
                leaseStore);
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{agentOptions.DeviceId}/heartbeat")
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(agentOptions.DeviceCredentialSecret))
            {
                message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, agentOptions.DeviceCredentialSecret);
            }

            var response = await client.SendAsync(message, stoppingToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: stoppingToken);
            if (heartbeat is not null)
            {
                await HandleHeartbeatCommandsAsync(client, heartbeat.Commands, stoppingToken);
            }

            var intervalSeconds = heartbeat?.HeartbeatIntervalSeconds ?? 10;

            logger.LogInformation("Heartbeat sent for {DeviceId}. Next heartbeat in {IntervalSeconds}s.", agentOptions.DeviceId, intervalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task HandleHeartbeatCommandsAsync(
        HttpClient client,
        IReadOnlyList<DeviceCommandDto> commands,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            var result = await commandHandler.HandleAsync(command, cancellationToken);
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/devices/{result.DeviceId}/commands/{result.CommandId}/result")
            {
                Content = JsonContent.Create(result)
            };

            var credentialSecret = options.Value.DeviceCredentialSecret;
            if (!string.IsNullOrWhiteSpace(credentialSecret))
            {
                message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
            }

            var response = await client.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();

            logger.LogInformation(
                "Heartbeat command {CommandId} acknowledged as {Status}.",
                command.CommandId,
                result.Status);
        }
    }

    private async Task TryReconcileSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runtimeState = runtimeStateStore.Current;
            var response = await sessionReconciliationReporter.ReportAsync(
                runtimeState.IsLocked,
                observedAtUtc: DateTimeOffset.UtcNow,
                cancellationToken);
            logger.LogInformation(
                "Session reconciliation returned {Action} for {SessionId}.",
                response.Action,
                response.SessionId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Session reconciliation failed. Continuing with heartbeat loop.");
        }
    }

    private async Task TryEnforceGraceModeAsync(CancellationToken cancellationToken)
    {
        try
        {
            await graceModeMonitor.EnforceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Grace mode enforcement failed. Continuing with heartbeat loop.");
        }
    }

    private async Task TryReportInstalledAppsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var apps = await installedAppInventoryCollector.CollectAsync(cancellationToken);
            await installedAppReporter.ReportAsync(apps, DateTimeOffset.UtcNow, cancellationToken);
            logger.LogInformation("Installed app inventory reported with {InstalledAppCount} apps.", apps.Count);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Installed app inventory report failed. Continuing with heartbeat loop.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await realtimeClient.DisposeAsync();
        await base.StopAsync(cancellationToken);
    }
}
