using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>What happened when a high-risk action was submitted (anti-fraud spec §5.2).</summary>
public enum MoneyActionRequestOutcome
{
    /// <summary>Within policy — executed immediately through the ledger.</summary>
    Executed,

    /// <summary>Over the approval threshold — held in <c>pending</c> for a second pair of eyes.</summary>
    PendingApproval,

    /// <summary>Would breach a cap — refused with a recoverable error.</summary>
    Rejected,
}

public sealed record MoneyActionRequestResult(
    MoneyActionRequestOutcome Outcome,
    Guid? ResultingLedgerEntryId,
    Guid? MoneyActionRequestId,
    string? Error,
    bool NotFound,
    bool Conflict)
{
    public static MoneyActionRequestResult Executed(Guid? ledgerEntryId) =>
        new(MoneyActionRequestOutcome.Executed, ledgerEntryId, null, null, false, false);

    public static MoneyActionRequestResult Pending(Guid requestId) =>
        new(MoneyActionRequestOutcome.PendingApproval, null, requestId, null, false, false);

    public static MoneyActionRequestResult Refused(string error) =>
        new(MoneyActionRequestOutcome.Rejected, null, null, error, false, false);

    public static MoneyActionRequestResult Failed(MoneyActionExecutionResult execution) =>
        new(MoneyActionRequestOutcome.Rejected, null, null, execution.Error, execution.NotFound, execution.Conflict);
}

public sealed record MoneyActionDecisionResult(
    bool Succeeded,
    Guid? ResultingLedgerEntryId,
    string? Error,
    bool NotFound,
    bool Conflict,
    bool Forbidden)
{
    public static MoneyActionDecisionResult Ok(Guid? ledgerEntryId) => new(true, ledgerEntryId, null, false, false, false);

    public static MoneyActionDecisionResult Invalid(string error) => new(false, null, error, false, false, false);

    public static MoneyActionDecisionResult Missing(string error) => new(false, null, error, true, false, false);

    public static MoneyActionDecisionResult RequestConflict(string error) => new(false, null, error, false, true, false);

    public static MoneyActionDecisionResult Denied(string error) => new(false, null, error, false, false, true);
}
