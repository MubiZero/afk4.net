using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Periodically runs <see cref="AutoProtectionRunner"/> to warn/lock sessions that
/// hit a fixed time-out or an open-tab credit limit.
/// </summary>
public sealed class AutoProtectionHostedService(
    IServiceProvider serviceProvider,
    AutoProtectionOptions options,
    TimeProvider timeProvider,
    ILogger<AutoProtectionHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    protected override string JobName => PlatformJobNames.AutoProtection;

    protected override TimeSpan Interval => options.TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<AutoProtectionRunner>();
        return runner.RunOnceAsync(cancellationToken);
    }
}
