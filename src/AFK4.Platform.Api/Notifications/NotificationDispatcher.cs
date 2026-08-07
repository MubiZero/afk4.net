using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Hosted background dispatcher. Mirrors <c>InvoiceGenerationHostedService</c>: poll on a fixed
/// interval, and per tick open a scope and run <see cref="NotificationDispatchRunner"/> over the
/// due rows. SMTP latency/outages never block a caller — delivery is fully decoupled (D10).
/// </summary>
public sealed class NotificationDispatcher(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    ILogger<NotificationDispatcher> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly NotificationOptions options = options.Value;

    protected override string JobName => PlatformJobNames.NotificationDispatch;

    protected override TimeSpan Interval => options.PollInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<NotificationDispatchRunner>();
        var dispatched = await runner.RunAsync(options.DispatchBatchSize, cancellationToken);
        if (dispatched > 0)
        {
            logger.LogInformation("Notification dispatch tick processed {Count} row(s).", dispatched);
        }

        return dispatched;
    }
}
