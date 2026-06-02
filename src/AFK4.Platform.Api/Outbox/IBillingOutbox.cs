using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Outbox;

/// <summary>
/// Persistence seam for the transactional billing outbox. <see cref="Enqueue"/> only adds the row to
/// the ambient change tracker — the <em>caller's</em> <c>SaveChangesAsync</c> commits it in the same
/// transaction as the ledger rows, which is the whole point. The dispatcher claims due rows, mutates
/// the returned (tracked) entities, and calls <see cref="SaveAsync"/>.
/// </summary>
public interface IBillingOutbox
{
    /// <summary>Stage an outbox row on the ambient context; committed by the caller's transaction.</summary>
    void Enqueue(OutboxMessageEntity message);

    /// <summary>Pending rows whose <see cref="OutboxMessageEntity.AvailableAtUtc"/> is due, oldest first.</summary>
    Task<IReadOnlyList<OutboxMessageEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);
}
