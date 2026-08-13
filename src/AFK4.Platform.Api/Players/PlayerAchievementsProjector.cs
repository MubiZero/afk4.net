using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Players;

/// Стаж игрока: уровень и достижения, посчитанные по уже закрытым визитам.
///
/// Ничего не хранится отдельно — всё считается из истории. Отдельная таблица «достижения
/// игрока» разошлась бы с историей на первом же исправлении визита задним числом, и тогда
/// экран уверенно показывал бы стаж, которого не было.
public static class PlayerAchievementsProjector
{
    /// Пороги уровней в минутах за ПК. Первый уровень — за факт прихода, дальше шаг растёт:
    /// разница между вторым и третьим уровнем должна ощущаться, а не набегать за один вечер.
    private static readonly long[] LevelThresholdsMinutes =
        [0, 5 * 60, 15 * 60, 30 * 60, 60 * 60, 120 * 60, 240 * 60, 480 * 60];

    private const int RegularVisits = 10;
    private const int VeteranVisits = 50;
    private const int NightOwlVisits = 5;
    private const int MarathonMinutes = 5 * 60;

    /// Ночь — это с 22:00 до 05:00 по часам клуба, а не по UTC: в Душанбе разница в пять часов
    /// превратила бы вечер в ночь и раздала бы «ночного жителя» всем подряд.
    private const int NightStartHour = 22;
    private const int NightEndHour = 5;

    public static async Task<PlayerAchievementsDto> GetAsync(
        PlatformDbContext dbContext,
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        var visits = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.PlayerAccountId == playerAccountId &&
                session.State == SessionStateNames.Ended &&
                session.StartedAtUtc != null &&
                session.EndedAtUtc != null)
            .Select(session => new
            {
                session.BranchId,
                StartedAtUtc = session.StartedAtUtc!.Value,
                EndedAtUtc = session.EndedAtUtc!.Value
            })
            .ToListAsync(cancellationToken);

        var branchIds = visits.Select(visit => visit.BranchId).Distinct().ToList();
        var timeZones = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branchIds.Contains(branch.BranchId))
            .Select(branch => new { branch.BranchId, branch.PreferredTimeZone })
            .ToDictionaryAsync(branch => branch.BranchId, branch => branch.PreferredTimeZone, cancellationToken);

        var ordered = visits.OrderBy(visit => visit.EndedAtUtc).ToList();
        long playedMinutes = 0;
        var nightVisits = 0;
        DateTimeOffset? marathonAt = null;
        DateTimeOffset? nightOwlAt = null;

        foreach (var visit in ordered)
        {
            var minutes = (long)Math.Round((visit.EndedAtUtc - visit.StartedAtUtc).TotalMinutes);
            if (minutes < 0)
            {
                minutes = 0;
            }

            playedMinutes += minutes;

            if (minutes >= MarathonMinutes)
            {
                marathonAt ??= visit.EndedAtUtc;
            }

            var localHour = ToClubHour(visit.StartedAtUtc, timeZones.GetValueOrDefault(visit.BranchId));
            if (localHour >= NightStartHour || localHour < NightEndHour)
            {
                nightVisits++;
                if (nightVisits == NightOwlVisits)
                {
                    nightOwlAt = visit.EndedAtUtc;
                }
            }
        }

        var firstReview = await dbContext.ClubReviews
            .AsNoTracking()
            .Where(review => review.PlayerAccountId == playerAccountId)
            .OrderBy(review => review.CreatedAtUtc)
            .Select(review => (DateTimeOffset?)review.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var visitCount = ordered.Count;
        DateTimeOffset? VisitNumber(int number) =>
            visitCount >= number ? ordered[number - 1].EndedAtUtc : null;

        var achievements = new List<PlayerAchievementDto>
        {
            new(PlayerAchievementCodes.FirstVisit, Math.Min(visitCount, 1), 1, VisitNumber(1)),
            new(PlayerAchievementCodes.Regular, Math.Min(visitCount, RegularVisits), RegularVisits,
                VisitNumber(RegularVisits)),
            new(PlayerAchievementCodes.Veteran, Math.Min(visitCount, VeteranVisits), VeteranVisits,
                VisitNumber(VeteranVisits)),
            new(PlayerAchievementCodes.NightOwl, Math.Min(nightVisits, NightOwlVisits), NightOwlVisits, nightOwlAt),
            new(PlayerAchievementCodes.Marathon, marathonAt is null ? 0 : 1, 1, marathonAt),
            new(PlayerAchievementCodes.Reviewer, firstReview is null ? 0 : 1, 1, firstReview)
        };

        var level = LevelFor(playedMinutes);
        var next = level < LevelThresholdsMinutes.Length
            ? LevelThresholdsMinutes[level] - playedMinutes
            : (long?)null;

        return new PlayerAchievementsDto(level, visitCount, playedMinutes, next, achievements);
    }

    /// Уровень — порядковый номер порога, который игрок уже перешагнул: 1 за первый визит,
    /// дальше по часам.
    private static int LevelFor(long playedMinutes)
    {
        var level = 0;
        foreach (var threshold in LevelThresholdsMinutes)
        {
            if (playedMinutes < threshold)
            {
                break;
            }

            level++;
        }

        return Math.Max(level, 1);
    }

    private static int ToClubHour(DateTimeOffset instant, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return instant.Hour;
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(instant, zone).Hour;
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            // Незнакомый идентификатор пояса — повод посчитать по UTC, а не уронить экран стажа.
            return instant.Hour;
        }
    }
}
