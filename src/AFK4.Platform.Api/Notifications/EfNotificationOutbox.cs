using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// EF Core implementation of <see cref="INotificationOutbox"/> over <see cref="PlatformDbContext"/>.
/// Idempotency is enforced by a query-then-insert plus the unique index on
/// <see cref="NotificationOutboxEntity.IdempotencyKey"/> (the index is the real backstop against a race;
/// the query collapses the common duplicate-trigger / retry case).
/// </summary>
public sealed class EfNotificationOutbox(PlatformDbContext db) : INotificationOutbox
{
    public async Task<NotificationOutboxAddResult> AddIfAbsentAsync(NotificationOutboxEntity row, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);

        var existing = await db.NotificationOutbox
            .FirstOrDefaultAsync(candidate => candidate.IdempotencyKey == row.IdempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return new NotificationOutboxAddResult(existing, Created: false);
        }

        db.NotificationOutbox.Add(row);
        await db.SaveChangesAsync(cancellationToken);
        return new NotificationOutboxAddResult(row, Created: true);
    }

    public async Task<IReadOnlyList<NotificationOutboxEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        var due = await db.NotificationOutbox
            .Where(row => row.Status == NotificationOutboxStatus.Pending && row.NextAttemptUtc <= now)
            .OrderBy(row => row.NextAttemptUtc)
            .Take(max)
            .ToListAsync(cancellationToken);

        foreach (var row in due)
        {
            row.Status = NotificationOutboxStatus.Sending;
        }

        await db.SaveChangesAsync(cancellationToken);
        return due;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
