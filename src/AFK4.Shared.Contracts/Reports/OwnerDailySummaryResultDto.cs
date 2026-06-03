namespace AFK4.Shared.Contracts.Reports;

// Anti-fraud §5.6: the owner's daily "watch the staff" digest — per-actor refunds, comps, manual
// corrections / debt write-offs, and shift discrepancies for a single branch-day.
public sealed record OwnerDailySummaryActorRowDto(
    Guid? ActorStaffUserId,
    string ActorDisplayName,
    int RefundCount,
    long RefundTotalMinorUnits,
    int CompCount,
    long CompValueMinorUnits,
    int ManualCorrectionCount,
    long ManualCorrectionTotalMinorUnits,
    int WriteOffCount,
    long WriteOffTotalMinorUnits,
    int DiscrepancyShiftCount,
    long DiscrepancyTotalMinorUnits);

public sealed record OwnerDailySummaryResultDto(
    DateOnly Date,
    string CurrencyCode,
    IReadOnlyList<OwnerDailySummaryActorRowDto> Rows,
    long TotalRefundMinorUnits,
    int TotalCompCount,
    long TotalCompValueMinorUnits,
    long TotalManualCorrectionMinorUnits,
    long TotalWriteOffMinorUnits,
    long TotalDiscrepancyMinorUnits);
