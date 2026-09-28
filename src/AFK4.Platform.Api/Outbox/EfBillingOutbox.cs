using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Persistence seam for the transactional billing outbox over <see cref="PlatformDbContext"/>.
/// <see cref="Enqueue"/> deliberately does not save: the row rides the caller's transaction so it
/// commits atomically with the ledger. The unique index on
/// <see cref="OutboxMessageEntity.IdempotencyKey"/> is the backstop against a duplicate enqueue.
/// The dispatcher claims due rows, mutates the returned (tracked) entities, and calls
/// <see cref="SaveAsync"/>.
/// </summary>
public sealed class EfBillingOutbox(PlatformDbContext db)
{
    /// <summary>Stage an outbox row on the ambient context; committed by the caller's transaction.</summary>
    public void Enqueue(OutboxMessageEntity message)
    {
        ArgumentNullException.ThrowIfNull(message);
        db.OutboxMessages.Add(message);
    }

    /// <summary>Pending rows whose <see cref="OutboxMessageEntity.AvailableAtUtc"/> is due, oldest first.</summary>
    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken) =>
        await db.OutboxMessages
            .Where(row => row.Status == OutboxMessageStatus.Pending && row.AvailableAtUtc <= now)
            .OrderBy(row => row.AvailableAtUtc)
            .Take(max)
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
