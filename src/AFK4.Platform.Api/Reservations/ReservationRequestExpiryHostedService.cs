using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Периодически зовёт <see cref="ReservationRequestExpiryRunner"/>: освобождает место и
/// возвращает деньги, если клуб не ответил на заявку в обещанный срок.
/// </summary>
public sealed class ReservationRequestExpiryHostedService(
    IServiceProvider serviceProvider,
    ReservationRequestExpiryOptions options,
    TimeProvider timeProvider,
    ILogger<ReservationRequestExpiryHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    protected override string JobName => PlatformJobNames.ReservationRequestExpiry;

    protected override TimeSpan Interval => options.TickInterval;

    protected override Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<ReservationRequestExpiryRunner>();
        return runner.RunOnceAsync(cancellationToken);
    }
}
