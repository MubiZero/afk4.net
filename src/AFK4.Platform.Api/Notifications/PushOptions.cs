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
