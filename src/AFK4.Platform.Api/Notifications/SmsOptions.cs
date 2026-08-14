namespace AFK4.Platform.Api.Notifications;

public sealed class SmsOptions
{
    public const string SectionName = "Sms";

    public string BaseUrl { get; set; } = "https://gateway.payom.tj";
    public string ApiToken { get; set; } = string.Empty;
    public string SenderName { get; set; } = "AFK4.NET";
    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>
    /// Наш ключ шаблона уведомления → идентификатор одобренного шаблона payom.
    /// Держатся в конфигурации, а не в коде, по одной причине: шаблон нельзя отредактировать
    /// после создания — исправление текста даёт новый идентификатор и новую модерацию, и мы
    /// должны уметь подменить его без выкладки.
    /// Задаётся как <c>Sms__TemplateIds__player.sign_in_code=…</c>.
    /// </summary>
    public Dictionary<string, string> TemplateIds { get; set; } = [];

    /// <summary>
    /// Значение <c>text-1</c> — «кто пишет». Берётся отсюда, а не из тела запроса: иначе
    /// достаточно подделать вызов, чтобы разослать SMS, подписанные чужим брендом.
    /// </summary>
    public string SenderLabel { get; set; } = "AFK4.NET";

    /// <summary>
    /// Сколько символов остаётся на <c>text-1</c> в шаблоне кода. Фиксированная часть вместе
    /// с шестизначным кодом съедает 28 символов из 70 в сегменте; 40 — с запасом. Кириллица
    /// уходит как UCS-2, и 71-й символ стоит как второе сообщение.
    /// </summary>
    public int SenderLabelMaxLength { get; set; } = 40;
}

public static class SmsClientRegistration
{
    public const string HttpClientName = "payom-sms";
}
