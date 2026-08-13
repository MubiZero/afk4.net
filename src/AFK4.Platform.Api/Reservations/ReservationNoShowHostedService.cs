using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Периодически зовёт <see cref="ReservationNoShowRunner"/>: возвращает деньги и освобождает место,
/// если игрок не пришёл в свою бронь.
/// </summary>
public sealed class ReservationNoShowHostedService(
    IServiceProvider serviceProvider,
    ReservationNoShowOptions options,
    TimeProvider timeProvider,
    ILogger<ReservationNoShowHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    protected override string JobName => PlatformJobNames.ReservationNoShow;

    protected override TimeSpan Interval => options.TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<ReservationNoShowRunner>();
        return runner.RunOnceAsync(cancellationToken);
    }
}
