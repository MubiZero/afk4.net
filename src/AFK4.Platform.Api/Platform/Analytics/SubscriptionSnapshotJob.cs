using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class SubscriptionSnapshotJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformAnalyticsOptions> options,
    ILogger<SubscriptionSnapshotJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformAnalyticsOptions options = options.Value;

    protected override string JobName => PlatformJobNames.SubscriptionSnapshots;

    protected override TimeSpan Interval => options.SnapshotInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        scopedServices.GetRequiredService<ISubscriptionSnapshotRunner>().RunAsync(GetUtcNow(), cancellationToken);
}
