using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Reports;

namespace AFK4.Platform.Api.Reports;

/// <summary>
/// Anti-fraud §5.6: pure aggregation of a single branch-day's high-risk money activity, grouped by
/// the staff member who performed it. Refunds, manual corrections and debt write-offs are read from
/// the ledger (their real signed amounts); comps are counted from the <c>session.comp</c> audit feed
/// (they carry no value until §5.4 checkout valuation lands — count only, deliberately); shift
/// discrepancies come from closed shifts. Money totals are magnitudes; the discrepancy total keeps
/// its sign (a shortfall stays negative).
/// </summary>
public static class OwnerDailySummaryAggregator
{
    public static OwnerDailySummaryResultDto Build(
        DateOnly date,
        string currencyCode,
        IReadOnlyCollection<LedgerEntryEntity> moneyEntries,
        IReadOnlyCollection<AuditRecordEntity> compAudits,
        IReadOnlyCollection<ShiftEntity> closedShifts,
        IReadOnlyDictionary<Guid, string> actorNames)
    {
        var accumulators = new Dictionary<Guid, Accumulator>();

        // Real staff ids are never Guid.Empty, so it safely keys the "System" (null actor) bucket.
        Accumulator For(Guid? actorStaffUserId)
        {
            var key = actorStaffUserId ?? Guid.Empty;
            if (!accumulators.TryGetValue(key, out var accumulator))
            {
                accumulator = new Accumulator(actorStaffUserId);
                accumulators[key] = accumulator;
            }

            return accumulator;
        }

        foreach (var entry in moneyEntries)
        {
            var accumulator = For(entry.CreatedByStaffUserId);
            var magnitude = Math.Abs(entry.AmountMinorUnits);
            if (IsRefund(entry))
            {
                accumulator.RefundCount++;
                accumulator.RefundTotalMinorUnits += magnitude;
            }
            else if (IsWriteOff(entry))
            {
                accumulator.WriteOffCount++;
                accumulator.WriteOffTotalMinorUnits += magnitude;
            }
            else
            {
                accumulator.ManualCorrectionCount++;
                accumulator.ManualCorrectionTotalMinorUnits += magnitude;
            }
        }

        foreach (var comp in compAudits)
        {
            For(comp.ActorStaffUserId).CompCount++;
        }

        foreach (var shift in closedShifts)
        {
            if (shift.DifferenceMinorUnits == 0)
            {
                continue;
            }

            var accumulator = For(shift.ClosedByStaffUserId);
            accumulator.DiscrepancyShiftCount++;
            accumulator.DiscrepancyTotalMinorUnits += shift.DifferenceMinorUnits;
        }

        var rows = accumulators.Values
            .Select(accumulator => accumulator.ToRow(actorNames))
            .OrderByDescending(row => Weight(row))
            .ThenBy(row => row.ActorDisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new OwnerDailySummaryResultDto(
            date,
            currencyCode,
            rows,
            rows.Sum(row => row.RefundTotalMinorUnits),
            rows.Sum(row => row.CompCount),
            rows.Sum(row => row.ManualCorrectionTotalMinorUnits),
            rows.Sum(row => row.WriteOffTotalMinorUnits),
            rows.Sum(row => row.DiscrepancyTotalMinorUnits));
    }

    private static bool IsRefund(LedgerEntryEntity entry) =>
        entry.EntryType is LedgerEntryTypeNames.Refund or LedgerEntryTypeNames.Reversal;

    private static bool IsWriteOff(LedgerEntryEntity entry) =>
        entry.EntryType == LedgerEntryTypeNames.ManualCorrection
        && string.Equals(entry.AccountType, LedgerAccountTypeNames.Debt, StringComparison.OrdinalIgnoreCase)
        && entry.AmountMinorUnits < 0;

    private static long Weight(OwnerDailySummaryActorRowDto row) =>
        row.RefundTotalMinorUnits
        + row.ManualCorrectionTotalMinorUnits
        + row.WriteOffTotalMinorUnits
        + Math.Abs(row.DiscrepancyTotalMinorUnits);

    private sealed class Accumulator(Guid? actorStaffUserId)
    {
        public int RefundCount;
        public long RefundTotalMinorUnits;
        public int CompCount;
        public int ManualCorrectionCount;
        public long ManualCorrectionTotalMinorUnits;
        public int WriteOffCount;
        public long WriteOffTotalMinorUnits;
        public int DiscrepancyShiftCount;
        public long DiscrepancyTotalMinorUnits;

        public OwnerDailySummaryActorRowDto ToRow(IReadOnlyDictionary<Guid, string> actorNames) =>
            new(
                actorStaffUserId,
                ResolveName(actorNames),
                RefundCount,
                RefundTotalMinorUnits,
                CompCount,
                ManualCorrectionCount,
                ManualCorrectionTotalMinorUnits,
                WriteOffCount,
                WriteOffTotalMinorUnits,
                DiscrepancyShiftCount,
                DiscrepancyTotalMinorUnits);

        private string ResolveName(IReadOnlyDictionary<Guid, string> actorNames)
        {
            if (actorStaffUserId is null)
            {
                return "System";
            }

            return actorNames.TryGetValue(actorStaffUserId.Value, out var displayName)
                && !string.IsNullOrWhiteSpace(displayName)
                ? displayName
                : actorStaffUserId.Value.ToString("N")[..8];
        }
    }
}
