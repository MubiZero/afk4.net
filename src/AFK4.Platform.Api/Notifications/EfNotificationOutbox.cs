using AFK4.Platform.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// EF Core implementation of <see cref="EfNotificationOutbox"/> over <see cref="PlatformDbContext"/>.
/// Idempotency is enforced by a query-then-insert plus the unique index on
/// <see cref="NotificationOutboxEntity.IdempotencyKey"/> (the index is the real backstop against a race;
/// the query collapses the common duplicate-trigger / retry case).
/// </summary>
public sealed class EfNotificationOutbox(PlatformDbContext db)
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

    /// <summary>
    /// Сколько строка «отправляется», прежде чем её можно подобрать снова. Процесс, умерший посреди
    /// отправки, раньше оставлял её в <c>Sending</c> навсегда: сообщение не уходило никогда.
    /// </summary>
    public static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Захват — условная запись на каждую строку, а не «прочитал и пометил». При выкладке старый и
    /// новый контейнер API работают одновременно: оба читали одни и те же строки, оба помечали их и
    /// оба отправляли — игрок получал SMS дважды. Теперь строку забирает тот, чья запись нашла её
    /// ещё свободной; второй получает ноль изменённых строк и проходит мимо.
    ///
    /// Аренда живёт в <see cref="NotificationOutboxEntity.NextAttemptUtc"/>: захват сдвигает его на
    /// <see cref="ClaimLease"/> вперёд, поэтому отдельная колонка не нужна.
    /// </summary>
    public async Task<IReadOnlyList<NotificationOutboxEntity>> ClaimDueAsync(DateTimeOffset now, int max, CancellationToken cancellationToken)
    {
        var candidates = await db.NotificationOutbox
            .AsNoTracking()
            .Where(row => (row.Status == NotificationOutboxStatus.Pending || row.Status == NotificationOutboxStatus.Sending)
                && row.NextAttemptUtc <= now)
            .OrderBy(row => row.NextAttemptUtc)
            .Take(max)
            .Select(row => row.NotificationOutboxId)
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

        return claimed.Count == 0 ? [] : await GetByIdsAsync(claimed, cancellationToken);
    }

    private async Task<bool> TryClaimAsync(Guid id, DateTimeOffset now, DateTimeOffset leaseUntil, CancellationToken cancellationToken)
    {
        if (!db.Database.IsRelational())
        {
            // In-memory провайдер условной записи не умеет; гонки в нём нет — тесты однопоточные,
            // одновременность проверяет Postgres-тест.
            var row = await db.NotificationOutbox.SingleAsync(candidate => candidate.NotificationOutboxId == id, cancellationToken);
            row.Status = NotificationOutboxStatus.Sending;
            row.NextAttemptUtc = leaseUntil;
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }

        var affected = await db.NotificationOutbox
            .Where(row => row.NotificationOutboxId == id
                && (row.Status == NotificationOutboxStatus.Pending || row.Status == NotificationOutboxStatus.Sending)
                && row.NextAttemptUtc <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, NotificationOutboxStatus.Sending)
                    .SetProperty(row => row.NextAttemptUtc, leaseUntil),
                cancellationToken);
        return affected == 1;
    }

    public async Task<IReadOnlyList<NotificationOutboxEntity>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await db.NotificationOutbox
            .Include(row => row.Attachments)
            .Where(row => ids.Contains(row.NotificationOutboxId))
            .ToListAsync(cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}
