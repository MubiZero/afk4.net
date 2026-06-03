using System.Globalization;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Reports;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Notifications;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Notifications;

public sealed class EfDailySummaryRunner(
    PlatformDbContext dbContext,
    IOrganizationOwnerResolver ownerResolver,
    INotificationService notifications) : IDailySummaryRunner
{
    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Summarize the UTC day that has fully ended: yesterday relative to `now`.
        var summaryDate = DateOnly.FromDateTime(now.UtcDateTime).AddDays(-1);
        var windowStart = new DateTimeOffset(summaryDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var windowEnd = windowStart.AddDays(1);

        var saleOrgIds = await dbContext.PosSales
            .Where(sale => sale.State == PosSaleStateNames.Paid
                && sale.PaidAtUtc >= windowStart && sale.PaidAtUtc < windowEnd)
            .Select(sale => sale.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var shiftOrgIds = await dbContext.Shifts
            .Where(shift => shift.State == ShiftStateNames.Closed
                && shift.ClosedAtUtc >= windowStart && shift.ClosedAtUtc < windowEnd)
            .Select(shift => shift.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // A day with refunds/comps but no sales or shift closes is exactly what the owner wants flagged.
        var moneyOrgIds = await dbContext.LedgerEntries
            .Where(entry => entry.CreatedAtUtc >= windowStart && entry.CreatedAtUtc < windowEnd
                && (entry.EntryType == LedgerEntryTypeNames.Refund
                    || entry.EntryType == LedgerEntryTypeNames.Reversal
                    || entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .Select(entry => entry.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var compOrgIds = await dbContext.AuditRecords
            .Where(record => record.Action == AuditActionNames.SessionComp
                && record.CreatedAtUtc >= windowStart && record.CreatedAtUtc < windowEnd)
            .Select(record => record.OrganizationId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var organizationIds = saleOrgIds
            .Union(shiftOrgIds)
            .Union(moneyOrgIds)
            .Union(compOrgIds)
            .ToList();

        var enqueued = 0;
        foreach (var organizationId in organizationIds)
        {
            if (await TrySummarizeAsync(organizationId, summaryDate, windowStart, windowEnd, cancellationToken))
            {
                enqueued++;
            }
        }

        return enqueued;
    }

    private async Task<bool> TrySummarizeAsync(
        Guid organizationId,
        DateOnly summaryDate,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd,
        CancellationToken cancellationToken)
    {
        var recipient = await ownerResolver.ResolveAsync(organizationId, cancellationToken);
        if (recipient is null)
        {
            return false;
        }

        var sales = await dbContext.PosSales
            .Where(sale => sale.OrganizationId == organizationId
                && sale.State == PosSaleStateNames.Paid
                && sale.PaidAtUtc >= windowStart && sale.PaidAtUtc < windowEnd)
            .ToListAsync(cancellationToken);

        var shifts = await dbContext.Shifts
            .Where(shift => shift.OrganizationId == organizationId
                && shift.State == ShiftStateNames.Closed
                && shift.ClosedAtUtc >= windowStart && shift.ClosedAtUtc < windowEnd)
            .ToListAsync(cancellationToken);

        var moneyEntries = await dbContext.LedgerEntries
            .Where(entry => entry.OrganizationId == organizationId
                && entry.CreatedAtUtc >= windowStart && entry.CreatedAtUtc < windowEnd
                && (entry.EntryType == LedgerEntryTypeNames.Refund
                    || entry.EntryType == LedgerEntryTypeNames.Reversal
                    || entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .ToListAsync(cancellationToken);

        var compAudits = await dbContext.AuditRecords
            .Where(record => record.OrganizationId == organizationId
                && record.Action == AuditActionNames.SessionComp
                && record.CreatedAtUtc >= windowStart && record.CreatedAtUtc < windowEnd)
            .ToListAsync(cancellationToken);

        var revenueMinorUnits = sales.Sum(sale => sale.TotalMinorUnits);
        var discrepancyTotalMinorUnits = shifts.Sum(shift => shift.DifferenceMinorUnits);
        var discrepancyCount = shifts.Count(shift => shift.DifferenceMinorUnits != 0);
        var currency = sales.FirstOrDefault()?.CurrencyCode
            ?? shifts.FirstOrDefault()?.CurrencyCode
            ?? moneyEntries.FirstOrDefault()?.CurrencyCode
            ?? string.Empty;

        // Reuse the §5.6 aggregator for the org-wide high-risk rollup (per-actor detail lives in the
        // on-demand owner-daily-summary report). Discrepancy stays in its own tokens above.
        var antiFraud = OwnerDailySummaryAggregator.Build(
            summaryDate, currency, moneyEntries, compAudits, [], new Dictionary<Guid, string>());

        var request = new NotificationRequest(
            TemplateKey: NotificationTemplateKeys.OwnerDailySummary,
            Category: NotificationCategory.Digest,
            Recipient: new NotificationRecipient(
                Locale: string.Empty,
                EmailAddress: recipient.Email,
                StaffUserId: recipient.StaffUserId),
            Tokens: new Dictionary<string, string>
            {
                ["displayName"] = recipient.DisplayName,
                ["organizationName"] = recipient.OrganizationName,
                ["date"] = summaryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["revenue"] = Money(revenueMinorUnits),
                ["salesCount"] = sales.Count.ToString(CultureInfo.InvariantCulture),
                ["currency"] = currency,
                ["shiftsClosed"] = shifts.Count.ToString(CultureInfo.InvariantCulture),
                ["discrepancyCount"] = discrepancyCount.ToString(CultureInfo.InvariantCulture),
                ["discrepancyTotal"] = Money(discrepancyTotalMinorUnits),
                ["refundTotal"] = Money(antiFraud.TotalRefundMinorUnits),
                ["compCount"] = antiFraud.TotalCompCount.ToString(CultureInfo.InvariantCulture),
                ["compValueTotal"] = Money(antiFraud.TotalCompValueMinorUnits),
                ["manualCorrectionTotal"] = Money(antiFraud.TotalManualCorrectionMinorUnits),
                ["writeOffTotal"] = Money(antiFraud.TotalWriteOffMinorUnits),
            },
            IdempotencyKey: $"daily-summary:{organizationId:N}:{summaryDate:yyyy-MM-dd}",
            OrganizationId: organizationId);

        await notifications.SendAsync(request, cancellationToken);
        return true;
    }

    private static string Money(long minorUnits) => (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
