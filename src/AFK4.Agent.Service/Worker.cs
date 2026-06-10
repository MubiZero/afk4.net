using System.Net.Http.Json;
using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Shell;
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
    IPlayerShellProcessSupervisor playerShellProcessSupervisor,
    IPlayerShellStatePublisher playerShellStatePublisher,
    IDeviceCommandHandler commandHandler,
    ISessionReconciliationReporter sessionReconciliationReporter,
    IInstalledAppInventoryCollector installedAppInventoryCollector,
    IInstalledAppReporter installedAppReporter,
    IOfflineGraceState offlineGraceState,
    ICommandResultOutbox commandResultOutbox,
    TimeProvider timeProvider) : BackgroundService
{
    private const int HeartbeatRetryIntervalSeconds = 10;

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
        await TryMaintainPlayerShellAsync(stoppingToken);
        await TryReconcileSessionAsync(stoppingToken);
        await TryReportInstalledAppsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TryEnforceGraceModeAsync(stoppingToken);
            await TryMaintainPlayerShellAsync(stoppingToken);
            var outcome = await TrySendHeartbeatAsync(client, stoppingToken);
            var delay = HeartbeatCadence.NextDelay(
                outcome.Succeeded,
                leaseStore.Current,
                outcome.IntervalSeconds,
                timeProvider.GetUtcNow(),
                Random.Shared.NextDouble());
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task<HeartbeatOutcome> TrySendHeartbeatAsync(HttpClient client, CancellationToken cancellationToken)
    {
        try
        {
            var agentOptions = options.Value;
            var runtimeState = runtimeStateStore.Current;
            var request = HeartbeatPayloadFactory.Create(
                agentOptions,
                runtimeState.IsLocked,
                timeProvider.GetUtcNow(),
                leaseStore);
            using var message = new HttpRequestMessage(HttpMethod.Post, $"/api/devices/{agentOptions.DeviceId}/heartbeat")
            {
                Content = JsonContent.Create(request)
            };

            if (!string.IsNullOrWhiteSpace(agentOptions.DeviceCredentialSecret))
            {
                message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, agentOptions.DeviceCredentialSecret);
            }

            var response = await client.SendAsync(message, cancellationToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: cancellationToken);
            if (heartbeat is not null)
            {
                // Stamp the contact on the agent's own clock (spec §8) so offline grace is measured from
                // when the network actually dropped, robust to absolute-clock drift on the gaming PC.
                var agentNowUtc = timeProvider.GetUtcNow();
                offlineGraceState.RecordSuccessfulContact(agentNowUtc, heartbeat.EffectiveGraceMinutes);
                WarnOnClockDrift(heartbeat.ServerTimeUtc, agentNowUtc);
                await HandleHeartbeatCommandsAsync(client, heartbeat.Commands, cancellationToken);
            }

            var intervalSeconds = heartbeat?.HeartbeatIntervalSeconds ?? HeartbeatRetryIntervalSeconds;

            logger.LogInformation("Heartbeat sent for {DeviceId}. Next heartbeat in {IntervalSeconds}s.", agentOptions.DeviceId, intervalSeconds);
            return new HeartbeatOutcome(Succeeded: true, intervalSeconds);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Heartbeat failed. Retrying without stopping the Agent Service.");
            return new HeartbeatOutcome(Succeeded: false, HeartbeatRetryIntervalSeconds);
        }
    }

    private readonly record struct HeartbeatOutcome(bool Succeeded, int IntervalSeconds);

    private void WarnOnClockDrift(DateTimeOffset serverTimeUtc, DateTimeOffset agentNowUtc)
    {
        var drift = ClockDriftCheck.Drift(serverTimeUtc, agentNowUtc);
        if (!ClockDriftCheck.IsExcessive(drift, ClockDriftCheck.WarningThreshold))
        {
            return;
        }

        // Lease refresh and the offline grace window both lean on the agent and server clocks agreeing.
        // We only log it (no behaviour change) — a fix would correct against ServerTimeUtc, but first we
        // need to know whether real fleets actually drift.
        logger.LogWarning(
            "Agent clock differs from server by {DriftSeconds:F0}s (server {ServerTimeUtc:o}, agent {AgentTimeUtc:o}). "
            + "Lease and offline-grace timing assume a synchronised clock — check NTP on this gaming PC.",
            drift.TotalSeconds,
            serverTimeUtc,
            agentNowUtc);
    }

    private async Task HandleHeartbeatCommandsAsync(
        HttpClient client,
        IReadOnlyList<DeviceCommandDto> commands,
        CancellationToken cancellationToken)
    {
        foreach (var command in commands)
        {
            // Persist the result locally the instant the command executes (spec §6.4), so a crash or a
            // network drop between execution and delivery never loses the ack. Delivery is attempted by
            // the drain below and retried on every subsequent heartbeat until the backend acks.
            var result = await commandHandler.HandleAsync(command, cancellationToken);
            commandResultOutbox.Enqueue(result);
        }

        await DrainCommandResultsAsync(client, cancellationToken);
    }

    private async Task DrainCommandResultsAsync(HttpClient client, CancellationToken cancellationToken)
    {
        foreach (var result in commandResultOutbox.Pending)
        {
            try
            {
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
                commandResultOutbox.Acknowledge(result.CommandId);

                logger.LogInformation(
                    "Heartbeat command {CommandId} acknowledged as {Status}.",
                    result.CommandId,
                    result.Status);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                // Idempotent on CommandId server-side; leave it queued and stop draining for now so the
                // results are re-POSTed in order on the next heartbeat once connectivity returns.
                logger.LogWarning(
                    exception,
                    "Command result {CommandId} delivery failed. Keeping it queued for the next heartbeat.",
                    result.CommandId);
                break;
            }
        }
    }

    private async Task TryReconcileSessionAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runtimeState = runtimeStateStore.Current;
            var response = await sessionReconciliationReporter.ReportAsync(
                runtimeState.IsLocked,
                observedAtUtc: timeProvider.GetUtcNow(),
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

    private async Task TryMaintainPlayerShellAsync(CancellationToken cancellationToken)
    {
        try
        {
            var runtimeState = runtimeStateStore.Current;
            await playerShellProcessSupervisor.EnsureRunningAsync(runtimeState, cancellationToken);
            await playerShellStatePublisher.PublishAsync(CreatePlayerShellState(runtimeState), cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Player Shell supervision or state publishing failed. Continuing with heartbeat loop.");
        }
    }

    private PlayerShellStateDto CreatePlayerShellState(AgentRuntimeState runtimeState)
    {
        var agentOptions = options.Value;
        var lease = leaseStore.Current;
        int? remainingSeconds = lease is null
            ? null
            : Math.Max(0, (int)(lease.ExpiresAtUtc - timeProvider.GetUtcNow()).TotalSeconds);

        var isGraceMode = string.Equals(runtimeState.State, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        var threshold = agentOptions.ShellWarningThresholdSeconds;

        return new PlayerShellStateDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            State: runtimeState.State,
            SessionId: lease?.SessionId ?? runtimeState.ActiveSessionId,
            LeaseExpiresAtUtc: lease?.ExpiresAtUtc ?? runtimeState.LeaseExpiresAtUtc,
            RemainingSeconds: remainingSeconds,
            IsOnline: true,
            IsGraceMode: isGraceMode,
            WarningThresholdSeconds: threshold,
            Message: CreatePlayerShellMessage(runtimeState),
            LauncherApps: [],
            Locale: agentOptions.PreferredLocale,
            WarningKind: PlayerShellWarning.Classify(runtimeState.State, remainingSeconds, threshold, isGraceMode),
            Branding: string.IsNullOrWhiteSpace(agentOptions.ClubName)
                ? null
                : new ShellBrandingDto(agentOptions.ClubName!, agentOptions.LogoUrl, agentOptions.AccentColor));
    }

    private static string CreatePlayerShellMessage(AgentRuntimeState runtimeState)
    {
        return runtimeState.State switch
        {
            PlayerShellStateNames.Active => "Session is active.",
            PlayerShellStateNames.Grace => "Connection lost. Active session continues within the signed lease.",
            PlayerShellStateNames.Ending => "Session is ending.",
            PlayerShellStateNames.Maintenance => "This PC is under maintenance.",
            PlayerShellStateNames.Offline => "Agent is offline.",
            PlayerShellStateNames.Error => "This PC needs operator attention.",
            _ => "This PC is locked."
        };
    }

    private async Task TryReportInstalledAppsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var apps = await installedAppInventoryCollector.CollectAsync(cancellationToken);
            await installedAppReporter.ReportAsync(apps, timeProvider.GetUtcNow(), cancellationToken);
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
