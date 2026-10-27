using AFK4.Platform.Api.Platform.Health;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>
/// Читает сообщения боту длинным опросом (getUpdates) и отвечает на /start с кодом привязки и /stop.
/// Опрос, а не вебхук: не нужен открытый маршрут с отдельным секретом, а API у платформы один.
/// ponytail: при двух экземплярах API Telegram отдаст getUpdates только одному (409) — тогда вебхук.
/// </summary>
public sealed class TelegramUpdatesJob(
    IServiceProvider serviceProvider,
    IOptions<TelegramOptions> options,
    TimeProvider timeProvider,
    ILogger<TelegramUpdatesJob> logger) : PlatformPeriodicJob(serviceProvider, timeProvider, logger)
{
    /// <summary>Самый долгий разрыв между прогонами: опрос держится до 25 с, без бота — тик раз в 30 с.</summary>
    public static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(30);

    // Подтверждённые сообщения Telegram больше не отдаёт; после перезапуска он повторит
    // неподтверждённые — разбор их безопасен: код разовый, /stop идемпотентен.
    private long offset;

    protected override string JobName => PlatformJobNames.TelegramUpdates;

    protected override TimeSpan Interval => options.Value.IsConfigured ? TimeSpan.FromSeconds(1) : TickInterval;

    protected override async Task<int> TickAsync(IServiceProvider scopedServices, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return 0;
        }

        var bot = scopedServices.GetRequiredService<ITelegramBotApi>();
        var dialog = scopedServices.GetRequiredService<TelegramBotDialog>();
        var messages = await bot.GetUpdatesAsync(offset, cancellationToken);
        foreach (var message in messages)
        {
            offset = message.UpdateId + 1;
            if (message.ChatId != 0)
            {
                await dialog.HandleAsync(message, cancellationToken);
            }
        }

        return messages.Count;
    }
}
