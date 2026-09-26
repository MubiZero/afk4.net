using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Devices;

/// <summary>Периодически зовёт <see cref="DeviceMaintenanceExpiryRunner"/>: забытый ПК не стоит открытым.</summary>
public sealed class DeviceMaintenanceExpiryHostedService(
    IServiceProvider serviceProvider,
    DeviceMaintenanceExpiryOptions options,
    TimeProvider timeProvider,
    ILogger<DeviceMaintenanceExpiryHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    protected override string JobName => PlatformJobNames.DeviceMaintenanceExpiry;

    protected override TimeSpan Interval => options.TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<DeviceMaintenanceExpiryRunner>();
        return runner.RunOnceAsync(cancellationToken);
    }
}
