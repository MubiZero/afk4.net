namespace AFK4.Shared.Contracts.Notifications;

/// <summary>
/// Two-tier categories (D4). <see cref="Transactional"/> always sends (OTP, password reset, invite,
/// invoice) and ignores opt-out; <see cref="Operational"/> and <see cref="Digest"/> honour recipient
/// preferences.
/// </summary>
public enum NotificationCategory
{
    Transactional,
    Operational,
    Digest,
}

/// <summary>Pluggable delivery channels (D8). Only <see cref="Email"/> is wired today; the others are seams.</summary>
public enum NotificationChannel
{
    Email,
    Sms,
    InApp,
}

/// <summary>
/// A resolved delivery target. Locale is BCP-47-ish (ru/en/tg) resolved upstream; address fields are
/// channel-specific. Staff/player ids provide audit linkage and a future in-app target.
/// </summary>
public sealed record NotificationRecipient(
    string Locale,
    string? EmailAddress = null,
    string? PhoneNumber = null,
    Guid? StaffUserId = null,
    Guid? PlayerAccountId = null);

/// <summary>
/// A file attached to a notification (e.g. a scheduled report CSV). The bytes are persisted on the
/// outbox row so retries and restarts replay the same payload without regenerating it. Only the email
/// channel emits attachments today; other channels ignore them.
/// </summary>
public sealed record NotificationAttachment(string FileName, string ContentType, byte[] Content);

/// <summary>
/// One notification to send. Features build this and call <see cref="INotificationService"/>; they
/// never touch channels, templates or SMTP. Money/dates inside <see cref="Tokens"/> are formatted to
/// display strings by the caller so the renderer stays presentation-pure.
/// </summary>
public sealed record NotificationRequest(
    string TemplateKey,
    NotificationCategory Category,
    NotificationRecipient Recipient,
    IReadOnlyDictionary<string, string> Tokens,
    string IdempotencyKey,
    IReadOnlyList<NotificationChannel>? PreferredChannels = null,
    Guid? OrganizationId = null,
    Guid? BranchId = null,
    IReadOnlyList<NotificationAttachment>? Attachments = null);

/// <summary>
/// The result of enqueuing a notification: the outbox row id(s) and whether new rows were created
/// (<c>false</c> when a duplicate <see cref="NotificationRequest.IdempotencyKey"/> collapsed the send).
/// </summary>
public sealed record NotificationHandle(IReadOnlyList<Guid> OutboxIds, bool Created);

/// <summary>The outcome of a user-waiting send (OTP / password reset) after its first dispatch attempt.</summary>
public sealed record NotificationDeliveryResult(NotificationHandle Handle, bool Delivered, string? Error);
