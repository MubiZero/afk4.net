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
    IDeviceCredentialStore credentialStore,
    IShellWarningStore shellWarningStore,
    TimeProvider timeProvider,
    IProcessPolicyEnforcer? processPolicyEnforcer = null,
    IPlatformClockSynchronizer? platformClockSynchronizer = null) : BackgroundService
{
    private const int HeartbeatRetryIntervalSeconds = 10;

    /// <summary>Когда инвентарь установленного софта отправляли в прошлый раз.</summary>
    private DateTimeOffset lastInstalledAppReportUtc = DateTimeOffset.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var agentOptions = options.Value;
        if (!agentOptions.IsConfigured)
        {
            logger.LogError(
                "Agent is UNCONFIGURED: bootstrap.json was missing or unreadable and no valid enrollment "
                + "was found (OrganizationId/BranchId/DeviceId or a non-localhost https PlatformBaseUrl). "
                + "Entering idle state — no heartbeats, realtime, or network beacons will be sent until the "
                + "device is re-enrolled.");
            return;
        }

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
        await TryEnforceProcessPolicyAsync(stoppingToken);
        await TryMaintainPlayerShellAsync(stoppingToken);
        await TryReconcileSessionAsync(stoppingToken);
        await TryReportInstalledAppsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TryEnforceGraceModeAsync(stoppingToken);
            await TryEnforceProcessPolicyAsync(stoppingToken);
            await TryMaintainPlayerShellAsync(stoppingToken);
            await TryReportInstalledAppsOnScheduleAsync(stoppingToken);
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

            var credentialSecret = credentialStore.Current;
            if (!string.IsNullOrWhiteSpace(credentialSecret))
            {
                message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialSecret);
            }

            var response = await client.SendAsync(message, cancellationToken);

            response.EnsureSuccessStatusCode();
            var heartbeat = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>(cancellationToken: cancellationToken);
            if (heartbeat is not null)
            {
                // Сначала поправка часов: всё, что считается ниже по времени, должно считаться уже
                // по времени платформы.
                platformClockSynchronizer?.Synchronize(heartbeat.ServerTimeUtc);
                WarnOnClockDrift();

                // Stamp the contact on the agent's own clock (spec §8) so offline grace is measured from
                // when the network actually dropped, robust to absolute-clock drift on the gaming PC.
                var agentNowUtc = timeProvider.GetUtcNow();
                offlineGraceState.RecordSuccessfulContact(agentNowUtc, heartbeat.EffectiveGraceMinutes);
                // Код для монитора приезжает с сердцебиением — оболочка покажет его, пока за ПК
                // никто не сидит. Пустой он у занятой машины: звать к ней некого.
                seatingCode = heartbeat.SeatingCode;
                // Последнее известное оформление переживает обрыв связи: логотип на экране не
                // должен мигать оттого, что сеть моргнула.
                if (heartbeat.Branding is not null)
                {
                    branding = heartbeat.Branding;
                }
                if (heartbeat.RotateCredential)
                {
                    await TryRotateCredentialAsync(client, agentOptions, cancellationToken);
                }

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

    /// <summary>
    /// Клуб попросил сменить ключ — меняем его сами и записываем новый на диск.
    ///
    /// Сбой здесь не ломает ничего: старый ключ продолжает работать, просьба на сервере остаётся,
    /// и следующее сердцебиение попробует снова. Именно поэтому смена не отзывает старый ключ
    /// мгновенно — иначе неудачная запись оставляла бы ПК без входа.
    /// </summary>
    private async Task TryRotateCredentialAsync(
        HttpClient client,
        AgentOptions agentOptions,
        CancellationToken cancellationToken)
    {
        try
        {
            using var message = new HttpRequestMessage(
                HttpMethod.Post,
                $"/api/devices/{agentOptions.DeviceId}/credentials/self-rotate")
            {
                Content = JsonContent.Create(new SelfRotateDeviceCredentialRequest(
                    agentOptions.OrganizationId,
                    agentOptions.BranchId,
                    agentOptions.DeviceId))
            };
            message.Headers.Add(DeviceCredentialHeaders.CredentialSecret, credentialStore.Current);

            var response = await client.SendAsync(message, cancellationToken);
            response.EnsureSuccessStatusCode();

            var rotated = await response.Content.ReadFromJsonAsync<RotateDeviceCredentialResponse>(
                cancellationToken: cancellationToken);
            if (rotated is null || string.IsNullOrWhiteSpace(rotated.CredentialSecret))
            {
                logger.LogWarning("Credential rotation returned no secret. Keeping the current credential.");
                return;
            }

            credentialStore.Update(rotated.CredentialSecret);
            logger.LogInformation(
                "Device credential rotated for {DeviceId}. New credential {CredentialId}.",
                agentOptions.DeviceId,
                rotated.CredentialId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(
                exception,
                "Credential rotation failed. The current credential still works; retrying on the next heartbeat.");
        }
    }

    private readonly record struct HeartbeatOutcome(bool Succeeded, int IntervalSeconds);

    /// <summary>
    /// Поправка уже применена — но машину с неверными часами всё равно надо чинить: до первого
    /// сердцебиения после включения она живёт по своим часам, и это тоже время сессии.
    /// </summary>
    private void WarnOnClockDrift()
    {
        var drift = platformClockSynchronizer?.Offset ?? TimeSpan.Zero;
        if (!ClockDriftCheck.IsExcessive(drift, ClockDriftCheck.WarningThreshold))
        {
            return;
        }

        logger.LogWarning(
            "This PC's own clock differs from the platform by {DriftSeconds:F0}s. Session timing now follows the "
            + "platform, but the machine still needs its clock fixed — check NTP on this gaming PC.",
            drift.TotalSeconds);
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

                var credentialSecret = credentialStore.Current;
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

    /// <summary>
    /// Закрыть то, чему на этой машине быть не должно (<see cref="AgentOptions.DeniedProcessNames"/>).
    ///
    /// Правило, исполнитель и проверка на него были написаны, а звать исполнителя было некому: в
    /// службе не осталось ни одного вызова. Клуб, вписавший в настройку обход киоска или чит,
    /// считал, что запрет работает, — и ничего не работало.
    /// </summary>
    private async Task TryEnforceProcessPolicyAsync(CancellationToken cancellationToken)
    {
        if (processPolicyEnforcer is null)
        {
            return;
        }

        try
        {
            await processPolicyEnforcer.EnforceAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Process policy enforcement failed. Continuing with heartbeat loop.");
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

    /// <summary>Код, который оболочка показывает на простаивающем экране. Приезжает с сердцебиением.</summary>
    private string? seatingCode;

    private ShellBrandingDto? branding;

    private PlayerShellStateDto CreatePlayerShellState(AgentRuntimeState runtimeState)
    {
        var agentOptions = options.Value;
        var lease = leaseStore.Current;
        int? remainingSeconds = lease is null
            ? null
            : Math.Max(0, (int)(lease.ExpiresAtUtc - timeProvider.GetUtcNow()).TotalSeconds);

        var isGraceMode = string.Equals(runtimeState.State, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        var threshold = agentOptions.ShellWarningThresholdSeconds;
        var sessionId = lease?.SessionId ?? runtimeState.ActiveSessionId;

        // Предупреждение живёт ровно столько, сколько сессия, к которой оно пришло.
        shellWarningStore.ForgetUnless(sessionId);

        // Локальная оценка сильнее: связь пропала или время на исходе — это состояние самой машины,
        // и оно важнее того, что сервер знал минуту назад. А вот когда локально «всё спокойно»
        // (открытый счёт: остатка секунд нет вовсе), на экран идёт предупреждение сервера — иначе
        // игрок узнаёт о долге по погасшему экрану.
        var localWarning = PlayerShellWarning.Classify(runtimeState.State, remainingSeconds, threshold, isGraceMode);
        var warningKind = string.Equals(localWarning, PlayerShellWarningKinds.None, StringComparison.Ordinal)
            ? shellWarningStore.Current?.Kind ?? PlayerShellWarningKinds.None
            : localWarning;

        return new PlayerShellStateDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            State: runtimeState.State,
            SessionId: sessionId,
            LeaseExpiresAtUtc: lease?.ExpiresAtUtc ?? runtimeState.LeaseExpiresAtUtc,
            RemainingSeconds: remainingSeconds,
            IsOnline: true,
            IsGraceMode: isGraceMode,
            WarningThresholdSeconds: threshold,
            Message: CreatePlayerShellMessage(runtimeState),
            SeatingCode: seatingCode,
            LauncherApps: CreateLauncherApps(agentOptions),
            Locale: agentOptions.PreferredLocale,
            WarningKind: warningKind,
            // Оформление приходит сердцебиением; значения из конфига остаются запасным вариантом
            // для первого запуска, пока сервер ещё не ответил ни разу.
            Branding: branding ?? (string.IsNullOrWhiteSpace(agentOptions.ClubName)
                ? null
                : new ShellBrandingDto(agentOptions.ClubName!, agentOptions.LogoUrl, agentOptions.AccentColor)));
    }

    /// <summary>
    /// Список игр, который видит игрок. Берётся из той же настройки, по которой агент решает,
    /// что ему разрешено запускать: два разных списка разошлись бы в первый же день. Пункт,
    /// исполняемого файла которого на машине нет, показывается недоступным, а не прячется —
    /// «игра была вчера, а сегодня её нет» должно быть видно и игроку, и клубу.
    /// </summary>
    private static IReadOnlyList<LauncherAppDto> CreateLauncherApps(AgentOptions agentOptions) =>
        agentOptions.LauncherApps
            .Where(app => app.IsEnabled
                && !string.IsNullOrWhiteSpace(app.AppId)
                && !string.IsNullOrWhiteSpace(app.ExecutablePath))
            .Select(app => new LauncherAppDto(
                AppId: app.AppId,
                DisplayName: string.IsNullOrWhiteSpace(app.DisplayName) ? app.AppId : app.DisplayName,
                Category: string.IsNullOrWhiteSpace(app.Category) ? "Games" : app.Category,
                IconUri: null,
                IsAvailable: File.Exists(app.ExecutablePath)))
            .ToList();

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

    /// <summary>
    /// Инвентарь по расписанию. Раньше он снимался ровно один раз, при старте службы: игру,
    /// поставленную днём, клуб видел только после перезагрузки машины — то есть обычно никогда.
    /// </summary>
    private async Task TryReportInstalledAppsOnScheduleAsync(CancellationToken cancellationToken)
    {
        var interval = InstalledAppReportSchedule.Interval(options.Value.InstalledAppReportIntervalMinutes);
        if (!InstalledAppReportSchedule.IsDue(lastInstalledAppReportUtc, timeProvider.GetUtcNow(), interval))
        {
            return;
        }

        await TryReportInstalledAppsAsync(cancellationToken);
    }

    private async Task TryReportInstalledAppsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var apps = await installedAppInventoryCollector.CollectAsync(cancellationToken);
            await installedAppReporter.ReportAsync(apps, timeProvider.GetUtcNow(), cancellationToken);
            lastInstalledAppReportUtc = timeProvider.GetUtcNow();
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
