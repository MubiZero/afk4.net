using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Отдельное задание, а не довесок к снимкам подписок: одно задание — одна запись прогона и одна
/// причина отказа. Иначе падение клубной свёртки экран здоровья объявил бы провалом снимков подписок.
/// </summary>
public sealed class BranchSnapshotJob(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<PlatformAnalyticsOptions> options,
    ILogger<BranchSnapshotJob> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly PlatformAnalyticsOptions options = options.Value;

    protected override string JobName => PlatformJobNames.BranchSnapshots;

    protected override TimeSpan Interval => options.SnapshotInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken) =>
        scopedServices.GetRequiredService<IBranchSnapshotRunner>().RunAsync(GetUtcNow(), cancellationToken);
}
