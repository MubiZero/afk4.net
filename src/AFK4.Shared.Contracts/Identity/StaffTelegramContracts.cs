namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Привязан ли Telegram сотрудника к боту AFK4.NET. <paramref name="BotConfigured"/> — есть ли бот у
/// платформы вообще: без него Панель не предлагает кнопку, которая кончилась бы ошибкой.
/// </summary>
public sealed record StaffTelegramStatusDto(bool BotConfigured, bool Linked, DateTimeOffset? LinkedAtUtc);

/// <summary>
/// Разовая ссылка на бота: человек открывает её на телефоне (Панель показывает QR), бот получает
/// код и привязывает этот чат. Код действует до <paramref name="ExpiresAtUtc"/>.
/// </summary>
public sealed record StaffTelegramLinkDto(string DeepLink, DateTimeOffset ExpiresAtUtc);

public static class StaffTelegramErrorCodeNames
{
    /// <summary>У платформы нет бота (не задан Telegram__BotToken).</summary>
    public const string NotConfigured = "telegram_not_configured";

    /// <summary>Telegram не ответил, когда спросили имя бота для ссылки.</summary>
    public const string Unreachable = "telegram_unreachable";
}
