namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// The outcome of assessing a high-risk money action against branch policy + the actor's caps and
/// spend-so-far this shift-day (anti-fraud spec §5.1/§5.3): the effective (write-off-classified)
/// action type, the guard's decision, the resolved policy, and the inputs that drove it.
/// </summary>
public sealed record MoneyActionAssessment(
    MoneyActionType EffectiveActionType,
    MoneyActionDecision Decision,
    MoneyActionPolicy Policy,
    long AmountMinorUnits,
    long SpentTodayMinorUnits);

/// <summary>
/// Resolves the live <see cref="MoneyActionAssessment"/> for a high-risk request: combines per-branch
/// thresholds (D3), the actor's per-role caps (D4/D5), and the actor's open-shift spend, then runs the
/// pure <see cref="MoneyActionGuard"/> to a decision. This is the bridge the approval workflow (§5.2)
/// calls before touching the ledger.
/// </summary>
public interface IMoneyActionPolicyResolver
{
    Task<MoneyActionAssessment> AssessAsync(
        Guid organizationId,
        Guid branchId,
        Guid actorStaffUserId,
        IReadOnlyCollection<string> actorRoleNames,
        MoneyActionType requestedActionType,
        string accountType,
        long signedAmountMinorUnits,
        CancellationToken cancellationToken);
}
