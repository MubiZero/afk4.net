namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>Бот AFK4.NET в Telegram. Токен — из окружения (<c>Telegram__BotToken</c>), в репозитории его нет.</summary>
public sealed class TelegramOptions
{
    public const string SectionName = "Telegram";

    public string BaseUrl { get; set; } = "https://api.telegram.org";

    public string BotToken { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Сколько сервер Telegram держит запрос getUpdates, если сообщений нет.</summary>
    public int PollTimeoutSeconds { get; set; } = 25;

    /// <summary>Сколько живёт разовая ссылка на бота из Панели.</summary>
    public TimeSpan LinkCodeLifetime { get; set; } = TimeSpan.FromMinutes(10);

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BotToken);
}
