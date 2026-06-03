namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// Resolves the effective per-branch money-control thresholds (anti-fraud spec D3/D6/D7) at the read
/// boundary: a per-branch override wins over the global default, mirroring <c>GraceLeasePolicy</c>.
/// Thresholds are clamped to be non-negative (a negative threshold is meaningless; 0 is valid and
/// means "everything needs approval" per the D3 fork). The comp threshold (D7) reuses the high-risk
/// approval threshold unless the branch overrides it.
/// </summary>
public static class MoneyControlPolicy
{
    /// <summary>D3 default high-risk approval threshold: 5000 minor units (50.00 TJS) per transaction.</summary>
    public const long DefaultApprovalThresholdMinorUnits = 5000;

    /// <summary>D6 default shift-close discrepancy tolerance: 2000 minor units (20.00 TJS).</summary>
    public const long DefaultDiscrepancyToleranceMinorUnits = 2000;

    public static long ResolveApprovalThreshold(long? branchOverrideMinorUnits, long globalDefaultMinorUnits) =>
        Math.Max(0, branchOverrideMinorUnits ?? globalDefaultMinorUnits);

    public static long ResolveCompThreshold(long? branchCompOverrideMinorUnits, long effectiveApprovalThresholdMinorUnits) =>
        Math.Max(0, branchCompOverrideMinorUnits ?? effectiveApprovalThresholdMinorUnits);

    public static long ResolveDiscrepancyTolerance(long? branchOverrideMinorUnits, long globalDefaultMinorUnits) =>
        Math.Max(0, branchOverrideMinorUnits ?? globalDefaultMinorUnits);
}
