using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Periodically runs <see cref="IScheduledReportRunner"/> so due report schedules deliver their CSV
/// to the org owner. Mirrors the daily-summary hosted service; the runner's per-(schedule, window)
/// idempotency makes the tick cadence non-critical.
/// </summary>
public sealed class ScheduledReportHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    ILogger<ScheduledReportHostedService> logger) : BackgroundService
{
    private readonly NotificationOptions options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled-report tick failed.");
            }

            try
            {
                await Task.Delay(options.ScheduledReportInterval, timeProvider, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TickAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var runner = scope.ServiceProvider.GetRequiredService<IScheduledReportRunner>();
        var now = timeProvider.GetUtcNow();
        var sent = await runner.RunAsync(now, cancellationToken);
        if (sent > 0)
        {
            logger.LogInformation("Scheduled-report tick enqueued {Count} report(s).", sent);
        }
    }
}
