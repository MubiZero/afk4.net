using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed record SnapshotRow(Guid OrganizationId, DateOnly SnapshotDate, string Status);

public sealed record MovementPoint(int Year, int Month, int Joined, int Left, int PayingAtMonthEnd);

/// <summary>
/// Приход и отток клубов по месяцам. Считается ТОЛЬКО из суточных снимков: подписка хранит лишь
/// сегодняшний статус, и клуб, ушедший в июне, сегодня неотличим от того, кто не платил никогда.
/// </summary>
public static class SubscriptionMovement
{
    /// <summary>Платящий = active или past_due: клуб, которому шлют напоминания, ещё не ушёл.</summary>
    private static bool IsPaying(string status) =>
        status == SubscriptionStatusNames.Active || status == SubscriptionStatusNames.PastDue;

    /// <param name="firstMonth">First day of the first calendar month in the window (inclusive).</param>
    /// <param name="lastMonth">First day of the last calendar month in the window (inclusive).</param>
    public static IReadOnlyList<MovementPoint> Compute(
        IReadOnlyCollection<SnapshotRow> snapshots, DateOnly firstMonth, DateOnly lastMonth)
    {
        ArgumentNullException.ThrowIfNull(snapshots);
        // Both bounds must be the first of the month: the loop below walks whole months, so a
        // mid-month value would silently shift the window without any visible symptom.
        if (firstMonth.Day != 1)
            throw new ArgumentOutOfRangeException(nameof(firstMonth), firstMonth, "Must be the first day of a month.");
        if (lastMonth.Day != 1)
            throw new ArgumentOutOfRangeException(nameof(lastMonth), lastMonth, "Must be the first day of a month.");

        // Последний снимок каждого клуба в каждом месяце — состояние на конец месяца.
        var monthEndStatus = snapshots
            .GroupBy(row => (row.OrganizationId, row.SnapshotDate.Year, row.SnapshotDate.Month))
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(row => row.SnapshotDate).First().Status);

        var clubs = snapshots.Select(row => row.OrganizationId).Distinct().ToList();
        var points = new List<MovementPoint>();

        for (var month = firstMonth; month <= lastMonth; month = month.AddMonths(1))
        {
            var previous = month.AddMonths(-1);
            var joined = 0;
            var left = 0;
            var paying = 0;

            foreach (var club in clubs)
            {
                var nowPaying = monthEndStatus.TryGetValue((club, month.Year, month.Month), out var current)
                    && IsPaying(current);
                var wasPaying = monthEndStatus.TryGetValue((club, previous.Year, previous.Month), out var before)
                    && IsPaying(before);

                if (nowPaying) paying++;
                if (nowPaying && !wasPaying) joined++;
                if (!nowPaying && wasPaying) left++;
            }

            points.Add(new MovementPoint(month.Year, month.Month, joined, left, paying));
        }

        return points;
    }
}
