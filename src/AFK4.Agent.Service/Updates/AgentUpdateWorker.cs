using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class AgentUpdateWorker(
    ILogger<AgentUpdateWorker> logger,
    IAgentUpdateCoordinator updateCoordinator,
    IUpdateRecoveryService recoveryService,
    IOptions<AgentOptions> options) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await recoveryService.RecoverAsync(stoppingToken);
        await TryApplyUpdatesAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(GetUpdateCheckInterval(), stoppingToken);
            await TryApplyUpdatesAsync(stoppingToken);
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
