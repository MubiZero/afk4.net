namespace AFK4.Platform.Api.Data;

/// <summary>
/// One queued notification delivery. The row is written before any channel is touched (D2) and is
/// the unit of retry, idempotency and audit. Channel / Category / Status are stored as strings to
/// match the surrounding persistence convention; the enum equivalents live in the contract layer.
/// </summary>
public sealed class NotificationOutboxEntity
{
    public Guid NotificationOutboxId { get; set; }

    /// <summary>Caller-supplied key (composed with the channel), unique — collapses duplicate triggers/retries (D3).</summary>
    public string IdempotencyKey { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    /// <summary>Rendered delivery target (email address / phone). In-app uses the linkage ids instead.</summary>
    public string RecipientAddress { get; set; } = string.Empty;

    public Guid? StaffUserId { get; set; }

    public Guid? PlayerAccountId { get; set; }

    public Guid? OrganizationId { get; set; }

    public Guid? BranchId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string BodyText { get; set; } = string.Empty;

    public string BodyHtml { get; set; } = string.Empty;

    /// <summary>
    /// Значения плейсхолдеров этого сообщения, как их дал вызывающий. Хранятся рядом с уже
    /// отрисованным текстом, потому что SMS-шлюз принимает не текст, а шаблон со значениями —
    /// и повтор через час должен отправить ровно те же, а не пересобирать их заново.
    /// </summary>
    public string? TokensJson { get; set; }

    public string Status { get; set; } = NotificationOutboxStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTimeOffset NextAttemptUtc { get; set; }

    public string? LastError { get; set; }

    public DateTimeOffset CreatedUtc { get; set; }

    public DateTimeOffset? SentUtc { get; set; }

    /// <summary>Files delivered with this notification (e.g. a scheduled report CSV). Persisted so retries replay the same payload.</summary>
    public List<NotificationOutboxAttachmentEntity> Attachments { get; set; } = [];
}

/// <summary>A file persisted alongside an outbox row and attached when the email channel delivers it.</summary>
public sealed class NotificationOutboxAttachmentEntity
{
    public Guid NotificationOutboxAttachmentId { get; set; }

    public Guid NotificationOutboxId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public byte[] Content { get; set; } = [];
}

/// <summary>The closed set of <see cref="NotificationOutboxEntity.Status"/> values.</summary>
public static class NotificationOutboxStatus
{
    public const string Pending = "Pending";
    public const string Sending = "Sending";
    public const string Sent = "Sent";
    public const string Failed = "Failed";
    public const string Suppressed = "Suppressed";
}
