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

    /// <summary>Сколько строка «отправляется», прежде чем её можно подобрать снова (процесс умер посреди отправки).</summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Due rows claimed by a conditional write per row, oldest first. Two API containers overlap during a
    /// deploy; reading Pending rows without claiming them let both dispatch the same message. A claim moves
    /// the row to <see cref="OutboxMessageStatus.Sending"/> and pushes <see cref="OutboxMessageEntity.AvailableAtUtc"/>
    /// out by <see cref="ClaimLease"/>, so a row whose dispatcher died is picked up again after the lease.
    /// </summary>
    public async Task<IReadOnlyList<OutboxMessageEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        var candidates = await db.OutboxMessages
            .AsNoTracking()
            .Where(row => (row.Status == OutboxMessageStatus.Pending || row.Status == OutboxMessageStatus.Sending)
                && row.AvailableAtUtc <= now)
            .OrderBy(row => row.AvailableAtUtc)
            .Take(max)
            .Select(row => row.OutboxMessageId)
            .ToListAsync(cancellationToken);

        var leaseUntil = now + ClaimLease;
        var claimed = new List<Guid>(candidates.Count);
        foreach (var id in candidates)
        {
            if (await TryClaimAsync(id, now, leaseUntil, cancellationToken))
            {
                claimed.Add(id);
            }
        }

        return claimed.Count == 0
            ? []
            : await db.OutboxMessages.Where(row => claimed.Contains(row.OutboxMessageId)).ToListAsync(cancellationToken);
    }

    private async Task<bool> TryClaimAsync(Guid id, DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            // In-memory has no conditional update and no concurrency; the Postgres test covers the race.
            var row = await db.OutboxMessages.SingleAsync(candidate => candidate.OutboxMessageId == id, cancellationToken);
            row.Status = OutboxMessageStatus.Sending;
            row.AvailableAtUtc = leaseUntil;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await db.OutboxMessages
            .Where(row => row.OutboxMessageId == id
                && (row.Status == OutboxMessageStatus.Pending || row.Status == OutboxMessageStatus.Sending)
                && row.AvailableAtUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, OutboxMessageStatus.Sending)
                    .SetProperty(row => row.AvailableAtUtc, leaseUntil),
                cancellationToken);
        return affected == 1;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
