using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// The single call-site for every feature that needs to talk to a recipient (D1). Resolves locale +
/// preferences, renders the template, and persists the send to the outbox (D2); the dispatcher
/// delivers. Features never touch channels, templates or SMTP directly.
/// </summary>
public interface INotificationService
{
    /// <summary>Queued: persist to the outbox and return immediately; the dispatcher delivers.</summary>
    Task<NotificationHandle> SendAsync(NotificationRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Convenience for user-waiting flows (OTP, password reset): still outbox-first, then awaits the
    /// first dispatch attempt so the UI can report success/failure immediately. A failed send leaves
    /// the row Pending for the background dispatcher to retry.
    /// </summary>
    Task<NotificationDeliveryResult> SendNowAsync(NotificationRequest request, CancellationToken cancellationToken);
}
