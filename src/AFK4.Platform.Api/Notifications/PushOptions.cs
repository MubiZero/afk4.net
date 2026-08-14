namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Доступы к FCM. Берутся из служебного ключа проекта Firebase: там это поля
/// <c>project_id</c>, <c>client_email</c> и <c>private_key</c>. Ключ секретный — он живёт в
/// переменных окружения сервера, а не в репозитории.
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string ProjectId { get; set; } = string.Empty;

    /// <summary>Адрес служебного аккаунта, от имени которого просим токен доступа.</summary>
    public string ClientEmail { get; set; } = string.Empty;

    /// <summary>Приватный ключ служебного аккаунта в формате PEM.</summary>
    public string PrivateKey { get; set; } = string.Empty;

    /// <summary>
    /// Ключ, пригодный для разбора. В служебном файле Firebase он записан одной строкой, где
    /// переносы закодированы как <c>\n</c>; при переносе в переменную окружения половина
    /// переносов обычно становится настоящими, а половина остаётся такими — и PEM не читается.
    /// Приводим к одному виду здесь, а не просим человека выправлять base64 руками.
    /// </summary>
    public string NormalizedPrivateKey => PrivateKey
        .Replace("\\r\\n", "\n", StringComparison.Ordinal)
        .Replace("\\n", "\n", StringComparison.Ordinal)
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Trim();

    public string TokenEndpoint { get; set; } = "https://oauth2.googleapis.com/token";

    public int TimeoutSeconds { get; set; } = 15;

    /// <summary>Настроен ли канал. Без ключей пуши не отправляются, но и падать сервер не должен.</summary>
    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ProjectId)
        && !string.IsNullOrWhiteSpace(ClientEmail)
        && !string.IsNullOrWhiteSpace(PrivateKey);
}

public static class PushClientRegistration
{
    public const string HttpClientName = "fcm-push";
}
