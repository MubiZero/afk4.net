using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Тикает <see cref="PlayerReminderRunner"/>. Интервал заметно короче окна поиска: пропущенный
/// тик не должен стоить игроку напоминания.
/// </summary>
public sealed class PlayerReminderHostedService(
    IServiceProvider serviceProvider,
    TimeProvider timeProvider,
    IOptions<NotificationOptions> options,
    ILogger<PlayerReminderHostedService> logger)
    : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    private readonly NotificationOptions options = options.Value;

    protected override string JobName => PlatformJobNames.PlayerReminders;

    protected override TimeSpan Interval => options.PlayerReminderInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        var runner = scopedServices.GetRequiredService<PlayerReminderRunner>();
        var queued = await runner.RunAsync(cancellationToken);
        if (queued > 0)
        {
            Log.LogInformation("Player reminder tick queued {Count} notification(s).", queued);
        }

        return queued;
    }
}
