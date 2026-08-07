using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Platform.Analytics;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

public sealed class EfBranchDynamicsService(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IBranchDynamicsService
{
    private const int MinDays = 7;
    private const int MaxDays = 90;
    private const int DefaultDays = 30;

    public async Task<BranchDynamicsDto?> GetAsync(
        Guid organizationId,
        Guid branchId,
        int days,
        CancellationToken cancellationToken)
    {
        var branch = await dbContext.Branches
            .AsNoTracking()
            .Where(branch => branch.BranchId == branchId && branch.OrganizationId == organizationId)
            .Select(branch => new { branch.PreferredTimeZone, branch.CreatedAtUtc })
            .FirstOrDefaultAsync(cancellationToken);
        if (branch is null) return null;

        // Снимки хранятся в местных сутках филиала (см. BranchDailySnapshotEntity.SnapshotDate) —
        // окно отчёта обязано считаться в том же поясе, иначе свежий день клуба восточнее UTC
        // молча выпадает из ответа, а ещё не наступивший день клуба западнее UTC ложно попадает
        // в MissingDayCount.
        var zone = BranchLocalTime.ResolveZone(branch.PreferredTimeZone);
        var window = days <= 0 ? DefaultDays : Math.Clamp(days, MinDays, MaxDays);
        var toDate = BranchLocalTime.LocalDate(timeProvider.GetUtcNow(), zone).AddDays(-1);
        var fromDate = toDate.AddDays(-(window - 1));

        var snapshots = await dbContext.BranchDailySnapshots
            .AsNoTracking()
            .Where(snapshot =>
                snapshot.BranchId == branchId &&
                snapshot.SnapshotDate >= fromDate &&
                snapshot.SnapshotDate <= toDate)
            .OrderBy(snapshot => snapshot.SnapshotDate)
            .ToListAsync(cancellationToken);

        var currencyCode = snapshots.FirstOrDefault()?.CurrencyCode ?? BranchRevenue.DefaultCurrencyCode;

        // BranchDailySnapshotBuilder никогда не создаёт снимки раньше рождения филиала (в его же
        // местных сутках) — сутки окна до этой даты не «пропущены», их не было вовсе. Считать их
        // пробелом наблюдения было бы тем же подлогом, против которого разделён AgentAlive на
        // false/null, только уровнем выше: fromDate остаётся честной границей ЗАПРОШЕННОГО окна,
        // а вот MissingDayCount считает пробел только там, где филиал уже существовал.
        var createdLocalDate = BranchLocalTime.LocalDate(branch.CreatedAtUtc, zone);
        var observableFromDate = createdLocalDate > fromDate ? createdLocalDate : fromDate;
        var observableDayCount = observableFromDate > toDate ? 0 : toDate.DayNumber - observableFromDate.DayNumber + 1;

        return new BranchDynamicsDto(
            organizationId,
            branchId,
            fromDate,
            toDate,
            new MoneyDto(currencyCode, snapshots.Sum(snapshot => snapshot.RevenueMinorUnits)),
            snapshots.Sum(snapshot => snapshot.SessionCount),
            snapshots.Count(snapshot => snapshot.AgentAlive == false),
            snapshots.Count(snapshot => snapshot.AgentAlive is null),
            observableDayCount - snapshots.Count,
            snapshots.Select(snapshot => new BranchDynamicsDayDto(
                snapshot.SnapshotDate,
                snapshot.SessionCount,
                new MoneyDto(snapshot.CurrencyCode, snapshot.RevenueMinorUnits),
                snapshot.ShiftOpenedCount,
                snapshot.AgentAlive)).ToList());
    }
}
