using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Periodically runs <see cref="IDailySummaryRunner"/> so each organization gets one owner daily
/// summary digest for the prior UTC day. Mirrors the billing generation hosted service; the runner's
/// per-(org, date) idempotency makes the tick cadence non-critical.
/// </summary>
public sealed class DailySummaryHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    ILogger<DailySummaryHostedService> logger) : BackgroundService
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
                logger.LogError(ex, "Daily owner-summary tick failed.");
            }

            try
            {
                await Task.Delay(options.DailySummaryInterval, timeProvider, stoppingToken);
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
        var runner = scope.ServiceProvider.GetRequiredService<IDailySummaryRunner>();
        var now = timeProvider.GetUtcNow();
        var sent = await runner.RunAsync(now, cancellationToken);
        if (sent > 0)
        {
            logger.LogInformation("Daily owner-summary tick enqueued {Count} summary(ies).", sent);
        }
    }
}
