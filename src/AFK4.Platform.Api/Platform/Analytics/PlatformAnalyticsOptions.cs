namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class PlatformAnalyticsOptions
{
    public const string ConfigurationSection = "Analytics";

    /// <summary>
    /// Как часто задание проверяет, есть ли снимок за прошедшие сутки. Чаще суток — не вред:
    /// уникальный ключ (организация, дата) делает повтор безобидным, а частый тик означает,
    /// что снимок появится вскоре после перезапуска процесса, а не через сутки.
    /// </summary>
    public TimeSpan SnapshotInterval { get; set; } = TimeSpan.FromHours(1);
}
