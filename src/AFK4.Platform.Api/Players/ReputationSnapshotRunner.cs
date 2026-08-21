using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reservations;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

/// <summary>Настройки пересчёта сетевой репутации.</summary>
public sealed class ReputationSnapshotOptions
{
    /// <summary>
    /// Раз в сутки — и это не «достаточно часто», а требование приватности. Живой счётчик
    /// показывал бы «+1» ровно в ту минуту, когда человек сел за ПК у соседа.
    /// </summary>
    public TimeSpan TickInterval { get; set; } = TimeSpan.FromHours(24);
}

/// <summary>
/// Пересчитывает суточный снимок сетевой репутации: сколько визитов и сколько неявок у человека
/// во всей сети. Считает фон, а не запрос, — иначе клуб, опрашивающий счётчик каждую минуту,
/// вычислил бы вечера человека у конкурента, не получив ни одного названия клуба.
///
/// Источники чисел на волне 1: визит — завершённая сессия на любом счёте этой личности; неявка —
/// бронь, снятая с причиной <c>no-show</c>. Отдельного состояния «не приехал» пока нет; когда оно
/// появится, меняется запрос здесь, а не контракт.
/// </summary>
public sealed class ReputationSnapshotRunner(
    PlatformDbContext dbContext,
    TimeProvider timeProvider)
{
    /// <summary>Завершённый визит: и обычное завершение, и сверенная задним числом смена.</summary>
    private static readonly string[] CompletedSessionStates =
    [
        SessionStateNames.Ended,
        SessionStateNames.Reconciled
    ];

    /// <summary>Один проход. Возвращает число личностей в снимке.</summary>
    public async Task<int> RunOnceAsync(CancellationToken cancellationToken)
    {
        var calculatedAtUtc = timeProvider.GetUtcNow();

        var visits = await CountByPersonAsync(
            from session in dbContext.Sessions.AsNoTracking()
            join account in dbContext.PlayerAccounts.AsNoTracking()
                on session.PlayerAccountId equals (Guid?)account.PlayerAccountId
            where account.PlatformPersonId != null && CompletedSessionStates.Contains(session.State)
            select account.PlatformPersonId,
            cancellationToken);

        var noShows = await CountByPersonAsync(
            from reservation in dbContext.Reservations.AsNoTracking()
            join account in dbContext.PlayerAccounts.AsNoTracking()
                on reservation.PlayerAccountId equals (Guid?)account.PlayerAccountId
            where account.PlatformPersonId != null
                && reservation.State == ReservationStateNames.Cancelled
                && reservation.CancelReason == ReservationNoShowRunner.CancelReason
            select account.PlatformPersonId,
            cancellationToken);

        var personIds = visits.Keys.Union(noShows.Keys).ToHashSet();
        if (personIds.Count == 0)
        {
            return 0;
        }

        var existing = await dbContext.PlatformReputationSnapshots
            .Where(row => personIds.Contains(row.PlatformPersonId))
            .ToDictionaryAsync(row => row.PlatformPersonId, cancellationToken);

        foreach (var personId in personIds)
        {
            if (!existing.TryGetValue(personId, out var snapshot))
            {
                snapshot = new PlatformReputationSnapshotEntity { PlatformPersonId = personId };
                dbContext.PlatformReputationSnapshots.Add(snapshot);
            }

            snapshot.NetworkVisits = visits.GetValueOrDefault(personId);
            snapshot.NetworkNoShows = noShows.GetValueOrDefault(personId);
            // Одно и то же время во всех строках прохода: «на когда посчитано» — величина сети,
            // а не человека, и разъехавшиеся отметки выдавали бы, у кого что пересчитывалось.
            snapshot.CalculatedAtUtc = calculatedAtUtc;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return personIds.Count;
    }

    private static async Task<Dictionary<Guid, int>> CountByPersonAsync(
        IQueryable<Guid?> personIds, CancellationToken cancellationToken)
    {
        var counted = await personIds
            .GroupBy(personId => personId)
            .Select(group => new { PersonId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return counted
            .Where(row => row.PersonId is not null)
            .ToDictionary(row => row.PersonId!.Value, row => row.Count);
    }
}
