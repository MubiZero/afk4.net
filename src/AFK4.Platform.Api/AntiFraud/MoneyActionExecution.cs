namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// A high-risk money action as submitted by an operator (anti-fraud spec §5.2). Carries everything
/// needed to (a) assess it against policy and (b) replay it verbatim through the existing billing
/// command on approval. Serialised into <c>MoneyActionRequestEntity.PayloadJson</c> for held requests.
/// </summary>
public sealed record MoneyActionCommand(
    MoneyActionType RequestedActionType,
    Guid PlayerAccountId,
    Guid? LedgerEntryId,
    string AccountType,
    long SignedAmountMinorUnits,
    string CurrencyCode,
    int QuantitySeconds,
    string Reason,
    string IdempotencyKey);

/// <summary>Result of running a money action through the underlying billing command.</summary>
public sealed record MoneyActionExecutionResult(
    bool Succeeded,
    Guid? ResultingLedgerEntryId,
    string? Error,
    bool NotFound,
    bool Conflict)
{
    public static MoneyActionExecutionResult Ok(Guid? resultingLedgerEntryId) =>
        new(true, resultingLedgerEntryId, null, false, false);

    public static MoneyActionExecutionResult Invalid(string error) => new(false, null, error, false, false);

    public static MoneyActionExecutionResult Missing(string error) => new(false, null, error, true, false);

    public static MoneyActionExecutionResult RequestConflict(string error) => new(false, null, error, false, true);
}

/// <summary>
/// Executes a money action through the verified ledger path. The Ef implementation delegates to
/// <c>IBillingCommandService</c> so ledger immutability, idempotency, currency and refund-cap checks
/// all run as the existing, tested code (anti-fraud spec §5.2). Held behind an interface so the
/// approval workflow's state machine can be unit-tested without standing up full billing context.
/// </summary>
public interface IMoneyActionExecutor
{
    Task<MoneyActionExecutionResult> ExecuteAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        MoneyActionType effectiveType,
        MoneyActionCommand command,
        CancellationToken cancellationToken);
}
