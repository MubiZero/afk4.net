using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// A delivery channel. The dispatcher selects the channel whose <see cref="Channel"/> matches the
/// outbox row and asks it to deliver the rendered snapshot. Email is implemented today; SMS and
/// in-app register later behind this same seam (D8) with no caller change.
/// </summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    Task<ChannelResult> SendAsync(NotificationOutboxEntity row, CancellationToken cancellationToken);
}
