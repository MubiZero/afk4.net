namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>Что бот отвечает человеку. Тексты — из тех же шаблонов уведомлений, на языке его Telegram.</summary>
public sealed class TelegramBotDialog(
    TelegramLinks links,
    ITelegramBotApi bot,
    EmbeddedTemplateProvider templates,
    NotificationRenderer renderer,
    ILogger<TelegramBotDialog> logger)
{
    private static readonly HashSet<string> Locales = ["ru", "tg", "en"];

    public async Task HandleAsync(TelegramIncomingMessage message, CancellationToken cancellationToken)
    {
        var locale = message.LanguageCode is { } code && Locales.Contains(code) ? code : "ru";
        var text = message.Text.Trim();
        var (command, argument) = Split(text);

        string reply;
        if (command == "/start" && argument.Length > 0)
        {
            var staff = await links.LinkAsync(argument, message.ChatId, cancellationToken);
            reply = staff is null
                ? Render(NotificationTemplateKeys.TelegramLinkExpired, locale, [])
                : Render(NotificationTemplateKeys.TelegramLinked, locale, new() { ["displayName"] = staff.DisplayName });
        }
        else if (command == "/stop")
        {
            await links.UnlinkChatAsync(message.ChatId, cancellationToken);
            reply = Render(NotificationTemplateKeys.TelegramUnlinked, locale, []);
        }
        else
        {
            reply = Render(NotificationTemplateKeys.TelegramLinkExpired, locale, []);
        }

        try
        {
            await bot.SendHtmlAsync(message.ChatId, reply, cancellationToken);
        }
        catch (TelegramApiException exception)
        {
            // Привязка уже записана; не ушёл только ответ — человек увидит результат в Панели.
            logger.LogWarning("Telegram reply was not delivered: {Reason}", exception.Message);
        }
    }

    private string Render(string templateKey, string locale, Dictionary<string, string> tokens)
    {
        var rendered = renderer.Render(templates.Get(templateKey, locale), tokens);
        return TelegramText.FromEmail(string.Empty, rendered.BodyHtml);
    }

    // «/start@AFK4Bot код» в группах — команда с именем бота; приводим к «/start».
    private static (string Command, string Argument) Split(string text)
    {
        var space = text.IndexOf(' ', StringComparison.Ordinal);
        var command = space < 0 ? text : text[..space];
        var at = command.IndexOf('@', StringComparison.Ordinal);
        if (at > 0)
        {
            command = command[..at];
        }

        return (command.ToLowerInvariant(), space < 0 ? string.Empty : text[(space + 1)..].Trim());
    }
}
