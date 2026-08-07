using AFK4.Platform.Api.Platform.Health;
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
    ILogger<DailySummaryHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly NotificationOptions options = options.Value;

    protected override string JobName => PlatformJobNames.DailySummary;

    protected override TimeSpan Interval => options.DailySummaryInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<IDailySummaryRunner>();
        var now = GetUtcNow();
        var sent = await runner.RunAsync(now, cancellationToken);
        if (sent > 0)
        {
            logger.LogInformation("Daily owner-summary tick enqueued {Count} summary(ies).", sent);
        }

        return sent;
    }
}
