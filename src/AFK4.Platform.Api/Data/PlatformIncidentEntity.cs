namespace AFK4.Platform.Api.Data;

/// <summary>
/// Одна открытая или закрытая проблема платформы. Ключ дедупликации держит инвариант
/// «один открытый инцидент на ключ» — повторное обнаружение двигает LastSeenAtUtc,
/// а не заводит вторую строку и второе письмо.
/// </summary>
public sealed class PlatformIncidentEntity
{
    public Guid PlatformIncidentId { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>Ключ дедупликации, например "job_overdue:invoice_generation".</summary>
    public string DedupKey { get; set; } = string.Empty;

    public string Severity { get; set; } = PlatformIncidentSeverityNames.Warning;

    /// <summary>Короткий машинный контекст (числа и идентификаторы), НЕ готовая фраза.</summary>
    public string DetailsJson { get; set; } = "{}";

    public DateTimeOffset OpenedAtUtc { get; set; }

    public DateTimeOffset LastSeenAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public DateTimeOffset? LastNotifiedAtUtc { get; set; }
}

public static class PlatformIncidentKindNames
{
    public const string JobOverdue = "job_overdue";
    public const string JobFailing = "job_failing";
    public const string NotificationQueueStuck = "notification_queue_stuck";
    public const string BillingOutboxStuck = "billing_outbox_stuck";
}

public static class PlatformIncidentSeverityNames
{
    public const string Warning = "warning";
    public const string Critical = "critical";
}
