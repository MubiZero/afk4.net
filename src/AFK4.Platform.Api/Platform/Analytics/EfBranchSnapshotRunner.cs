using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Собирает суточные снимки филиалов. Запросов фиксированное число — по одному на источник за всё
/// окно досъёмки, а не по одному на клуб: правило пульса действует и здесь.
/// </summary>
public sealed class EfBranchSnapshotRunner(PlatformDbContext dbContext, TimeProvider timeProvider)
    : IBranchSnapshotRunner
{
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var branches = await dbContext.Branches
            .AsNoTracking()
            .Select(branch => new BranchSnapshotBranch(
                branch.BranchId, branch.OrganizationId, branch.PreferredTimeZone, branch.CreatedAtUtc))
            .ToListAsync(cancellationToken);
        if (branches.Count == 0) return 0;

        var lastSnapshotDates = await dbContext.BranchDailySnapshots
            .AsNoTracking()
            .GroupBy(snapshot => snapshot.BranchId)
            .Select(group => new { BranchId = group.Key, LastDate = group.Max(snapshot => snapshot.SnapshotDate) })
            .ToDictionaryAsync(row => row.BranchId, row => row.LastDate, cancellationToken);

        // Окно с запасом в сутки по обе стороны: границы местных суток сдвинуты относительно UTC,
        // и строка, попадающая в первый снимаемый день по местному времени, в UTC может лежать
        // за его пределами.
        var windowStart = now.AddDays(-(BranchDailySnapshotBuilder.MaxBackfillDays + 2));
        var windowEnd = now.AddDays(1);

        var sessionStarts = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.StartedAtUtc != null &&
                session.StartedAtUtc >= windowStart &&
                session.StartedAtUtc <= windowEnd)
            .Select(session => new BranchSnapshotEvent(session.BranchId, session.StartedAtUtc!.Value))
            .ToListAsync(cancellationToken);

        var payments = await dbContext.Payments
            .AsNoTracking()
            .Where(payment => payment.CreatedAtUtc >= windowStart && payment.CreatedAtUtc <= windowEnd)
            .Select(payment => new BranchSnapshotMoney(
                payment.BranchId, payment.CreatedAtUtc, payment.PaymentKind, payment.AmountMinorUnits, payment.CurrencyCode))
            .ToListAsync(cancellationToken);

        var ledgerEntries = await dbContext.LedgerEntries
            .AsNoTracking()
            .Where(entry =>
                entry.CreatedAtUtc >= windowStart &&
                entry.CreatedAtUtc <= windowEnd &&
                (entry.EntryType == LedgerEntryTypeNames.GameplayCharge ||
                    entry.EntryType == LedgerEntryTypeNames.PostpaidDebt ||
                    entry.EntryType == LedgerEntryTypeNames.Refund))
            .Select(entry => new BranchSnapshotMoney(
                entry.BranchId, entry.CreatedAtUtc, entry.EntryType, entry.AmountMinorUnits, entry.CurrencyCode))
            .ToListAsync(cancellationToken);

        var shiftOpens = await dbContext.Shifts
            .AsNoTracking()
            .Where(shift => shift.OpenedAtUtc >= windowStart && shift.OpenedAtUtc <= windowEnd)
            .Select(shift => new BranchSnapshotEvent(shift.BranchId, shift.OpenedAtUtc))
            .ToListAsync(cancellationToken);

        var heartbeats = await dbContext.Devices
            .AsNoTracking()
            .Where(device => device.LastHeartbeatAtUtc != null)
            .GroupBy(device => device.BranchId)
            .Select(group => new { BranchId = group.Key, Last = group.Max(device => device.LastHeartbeatAtUtc!.Value) })
            .ToDictionaryAsync(row => row.BranchId, row => row.Last, cancellationToken);

        var facts = BranchDailySnapshotBuilder.Build(new BranchSnapshotInput(
            now, branches, lastSnapshotDates, sessionStarts, payments, ledgerEntries, shiftOpens, heartbeats));
        if (facts.Count == 0) return 0;

        var createdAt = timeProvider.GetUtcNow();
        foreach (var fact in facts)
        {
            dbContext.BranchDailySnapshots.Add(new BranchDailySnapshotEntity
            {
                BranchDailySnapshotId = Guid.NewGuid(),
                OrganizationId = fact.OrganizationId,
                BranchId = fact.BranchId,
                SnapshotDate = fact.Date,
                SessionCount = fact.SessionCount,
                RevenueMinorUnits = fact.RevenueMinorUnits,
                CurrencyCode = fact.CurrencyCode,
                ShiftOpenedCount = fact.ShiftOpenedCount,
                AgentAlive = fact.AgentAlive,
                CreatedAtUtc = createdAt
            });
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return facts.Count;
    }
}
