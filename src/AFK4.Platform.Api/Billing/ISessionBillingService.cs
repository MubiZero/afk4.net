namespace AFK4.Platform.Api.Billing;

public sealed record SessionBillingValidationResult(
    bool Succeeded,
    string? Error,
    string TariffRuleVersionId,
    Guid? TariffVersionId,
    int BillableSeconds,
    long AmountMinorUnits,
    string CurrencyCode);

public interface ISessionBillingService
{
    Task<SessionBillingValidationResult> ValidateStartAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int durationMinutes,
        CancellationToken cancellationToken);

    Task<SessionBillingValidationResult> ValidateExtendAsync(
        Guid organizationId,
        Guid branchId,
        Guid? playerAccountId,
        string billingMode,
        Guid? tariffVersionId,
        Guid? playerPackageId,
        int additionalMinutes,
        CancellationToken cancellationToken);

    Task AppendStartLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task AppendExtendLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        Guid? playerPackageId,
        string billingMode,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Compute the time charge for an open-tab postpaid session at checkout from
    /// the elapsed time (<c>StartedAtUtc → now</c>) and the session's tariff. A
    /// guest/unbilled session yields a zero charge.
    /// </summary>
    Task<SessionBillingValidationResult> ComputeCheckoutChargeAsync(
        Guid sessionId,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>
    /// Anti-fraud §5.4: the would-be charge for a comp (free) session at the standard
    /// tariff for a fixed duration. Pure valuation — no wallet/shift checks and no
    /// ledger effect; a comp never bills. Used to gate and surface the comp value.
    /// </summary>
    Task<SessionBillingValidationResult> ComputeCompValueAsync(
        Guid organizationId,
        Guid branchId,
        Guid tariffVersionId,
        int durationMinutes,
        CancellationToken cancellationToken);

    /// <summary>
    /// Write the deferred time-charge ledger entry for an open-tab postpaid
    /// session at checkout. A zero charge writes nothing.
    /// </summary>
    Task AppendCheckoutLedgerEntriesAsync(
        Guid sessionId,
        Guid actorStaffUserId,
        SessionBillingValidationResult validation,
        Guid playerAccountId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
