using System.Globalization;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>Доставка уведомления в чат сотрудника с ботом AFK4.NET.</summary>
public sealed class TelegramChannel(ITelegramBotApi bot, IOptions<TelegramOptions> options) : INotificationChannel
{
    public NotificationChannel Channel => NotificationChannel.Telegram;

    public async Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        if (!options.Value.IsConfigured)
        {
            return ChannelResult.PermanentFailure("Telegram bot is not configured (set Telegram__BotToken).");
        }

        if (!long.TryParse(row.RecipientAddress, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chatId))
        {
            return ChannelResult.PermanentFailure("Telegram chat id is missing.");
        }

        try
        {
            await bot.SendHtmlAsync(chatId, TelegramText.FromEmail(row.Subject, row.BodyHtml), cancellationToken);
            return ChannelResult.Sent();
        }
        catch (TelegramApiException exception)
        {
            return exception.IsPermanent
                ? ChannelResult.PermanentFailure(exception.Message)
                : ChannelResult.TransientFailure(exception.Message);
        }
    }
}
