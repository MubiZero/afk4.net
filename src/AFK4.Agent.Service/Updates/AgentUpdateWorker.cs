using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class AgentUpdateWorker(
    ILogger<AgentUpdateWorker> logger,
    IAgentUpdateCoordinator updateCoordinator,
    IUpdateRecoveryService recoveryService,
    IOptions<AgentOptions> options,
    IOrganizationAdminProcessLauncher? organizationAdminProcessLauncher = null) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsConfigured)
        {
            logger.LogError(
                "Agent is UNCONFIGURED: bootstrap.json was missing or unreadable. Update loop is idle — "
                + "no update checks will be made until the device is re-enrolled.");
            return;
        }

        await TryRecoverAsync(stoppingToken);
        await TryLaunchOrganizationAdminAsync(stoppingToken);
        await TryApplyUpdatesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetUpdateCheckInterval(), stoppingToken);
            await TryLaunchOrganizationAdminAsync(stoppingToken);
            await TryApplyUpdatesAsync(stoppingToken);
        }
    }

    /// <summary>
    /// Разбор прерванной установки не должен стоить клубу цикла обновлений: раньше любая ошибка
    /// внутри восстановления вылетала из <c>ExecuteAsync</c>, и служба переставала проверять
    /// обновления до следующего перезапуска — молча, потому что фоновые сбои не роняют сервис.
    /// </summary>
    private async Task TryRecoverAsync(CancellationToken cancellationToken)
    {
        try
        {
            await recoveryService.RecoverAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(
                exception,
                "Recovery of an interrupted update failed. Continuing with the update loop so the device stays reachable for the next rollout.");
        }
    }

    private async Task TryLaunchOrganizationAdminAsync(CancellationToken cancellationToken)
    {
        if (organizationAdminProcessLauncher is null) return;
        try
        {
            await organizationAdminProcessLauncher.LaunchPendingAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Pending Organization Admin relaunch failed and will remain queued for retry.");
        }
    }

    private TimeSpan GetUpdateCheckInterval()
    {
        return TimeSpan.FromSeconds(Math.Max(1, options.Value.UpdateCheckIntervalSeconds));
    }

    private async Task TryApplyUpdatesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await updateCoordinator.CheckAndApplyUpdatesAsync(cancellationToken);
            logger.LogInformation(
                "Update check completed. Offered: {OfferedCount}, applied: {AppliedCount}, failed: {FailedCount}.",
                result.OfferedCount,
                result.AppliedCount,
                result.FailedCount);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Agent update check or execution failed. Continuing update loop.");
        }
    }
}
