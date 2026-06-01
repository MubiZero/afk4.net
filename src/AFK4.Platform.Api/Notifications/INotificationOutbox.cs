using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Persistence seam for the notification delivery outbox. Inserts are idempotent on the row's
/// <see cref="NotificationOutboxEntity.IdempotencyKey"/>; the dispatcher claims due rows, mutates
/// the returned (tracked) entities, and calls <see cref="SaveAsync"/>.
/// </summary>
public interface INotificationOutbox
{
    Task<NotificationOutboxAddResult> AddIfAbsentAsync(NotificationOutboxEntity row, CancellationToken cancellationToken);

    Task<IReadOnlyList<NotificationOutboxEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}

public sealed record NotificationOutboxAddResult(NotificationOutboxEntity Row, bool Created);
