using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Players;

/// <summary>
/// Раз в сутки зовёт <see cref="ReputationSnapshotRunner"/>. Суточный шаг — не экономия, а
/// граница приватности: между двумя снимками разница ответов ничего не рассказывает о том, где
/// человек провёл вечер.
/// </summary>
public sealed class ReputationSnapshotHostedService(
    IServiceProvider serviceProvider,
    ReputationSnapshotOptions options,
    TimeProvider timeProvider,
    ILogger<ReputationSnapshotHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    protected override string JobName => PlatformJobNames.ReputationSnapshot;

    protected override TimeSpan Interval => options.TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<ReputationSnapshotRunner>();
        return runner.RunOnceAsync(cancellationToken);
    }
}
