using AFK4.Platform.Api.Platform.Health;
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
    ILogger<ScheduledReportHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly NotificationOptions options = options.Value;

    protected override string JobName => PlatformJobNames.ScheduledReports;

    protected override TimeSpan Interval => options.ScheduledReportInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<IScheduledReportRunner>();
        var now = GetUtcNow();
        var sent = await runner.RunAsync(now, cancellationToken);
        if (sent > 0)
        {
            logger.LogInformation("Scheduled-report tick enqueued {Count} report(s).", sent);
        }

        return sent;
    }
}
