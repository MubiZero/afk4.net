namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// The result of substituting tokens into a <see cref="NotificationTemplate"/>. This is the
/// immutable snapshot persisted on the outbox row at enqueue time.
/// </summary>
public sealed record RenderedNotification(string Subject, string BodyText, string BodyHtml);
