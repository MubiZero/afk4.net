using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Hosted background dispatcher for the billing outbox. Mirrors <c>NotificationDispatcher</c>: poll on
/// a fixed interval, and per tick open a scope and run <see cref="OutboxDispatchRunner"/> over the due
/// rows. Downstream effects (lock notify, settlement confirmation) are decoupled from the committing
/// command, so a slow/failing effect never blocks or unwinds the money write.
/// </summary>
public sealed class OutboxDispatcher(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<OutboxOptions> options,
    ILogger<OutboxDispatcher> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly OutboxOptions options = options.Value;

    protected override string JobName => PlatformJobNames.BillingOutbox;

    protected override TimeSpan Interval => options.PollInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<OutboxDispatchRunner>();
        var dispatched = await runner.RunAsync(options.DispatchBatchSize, cancellationToken);
        if (dispatched > 0)
        {
            logger.LogInformation("Billing outbox dispatch tick processed {Count} row(s).", dispatched);
        }

        return dispatched;
    }
}
