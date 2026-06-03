using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// The high-risk money action a request represents. <see cref="DebtWriteOff"/> is a logical
/// refinement of <see cref="ManualCorrection"/> — a correction that *reduces* a player's debt with
/// no matching cash in — and is the highest-risk path (anti-fraud spec §5.1).
/// </summary>
public enum MoneyActionType
{
    Refund,
    ManualCorrection,
    DebtWriteOff,
}

/// <summary>The guard's verdict for a single high-risk request (anti-fraud spec §5.1).</summary>
public enum MoneyActionDecision
{
    /// <summary>Within threshold and caps — proceed to the existing ledger path unchanged.</summary>
    ExecuteNow,

    /// <summary>Over the approval threshold (D3) — hold for a second pair of eyes; no ledger write yet.</summary>
    RequireApproval,

    /// <summary>Would breach a per-transaction or daily cap — a clear, recoverable error.</summary>
    Reject,
}

/// <summary>
/// Per-actor money-action policy for a branch: the approval threshold (D3) plus the actor's
/// per-transaction (D4) and daily (D5) caps. Null caps mean unlimited (managers/owners, D4).
/// </summary>
public sealed record MoneyActionPolicy(
    long ApprovalThresholdMinorUnits,
    long? PerTransactionCapMinorUnits,
    long? DailyCapMinorUnits);

/// <summary>
/// Pure, deterministic policy core for high-risk money actions (anti-fraud spec §5.1). Given an
/// absolute amount, the actor's spend-so-far this shift-day, and the resolved policy, it returns
/// exactly one of <see cref="MoneyActionDecision"/>. No DB, no I/O — fully unit-testable in isolation.
/// Caps are hard walls (⇒ <see cref="MoneyActionDecision.Reject"/>); the approval threshold only
/// escalates to a second pair of eyes (⇒ <see cref="MoneyActionDecision.RequireApproval"/>).
/// </summary>
public static class MoneyActionGuard
{
    public static MoneyActionDecision Evaluate(long amountMinorUnits, long spentTodayMinorUnits, MoneyActionPolicy policy)
    {
        if (policy.PerTransactionCapMinorUnits is { } perTxnCap && amountMinorUnits > perTxnCap)
        {
            return MoneyActionDecision.Reject;
        }

        if (policy.DailyCapMinorUnits is { } dailyCap && checked(spentTodayMinorUnits + amountMinorUnits) > dailyCap)
        {
            return MoneyActionDecision.Reject;
        }

        return amountMinorUnits > policy.ApprovalThresholdMinorUnits
            ? MoneyActionDecision.RequireApproval
            : MoneyActionDecision.ExecuteNow;
    }

    /// <summary>
    /// Classifies a requested action for policy/audit purposes: a <see cref="MoneyActionType.ManualCorrection"/>
    /// that reduces a <c>debt</c> account (negative signed amount, i.e. forgiveness with no cash in) is a
    /// <see cref="MoneyActionType.DebtWriteOff"/>. Everything else passes through unchanged.
    /// </summary>
    public static MoneyActionType Classify(MoneyActionType requestedType, string accountType, long signedAmountMinorUnits) =>
        requestedType == MoneyActionType.ManualCorrection
        && string.Equals(accountType, LedgerAccountTypeNames.Debt, StringComparison.OrdinalIgnoreCase)
        && signedAmountMinorUnits < 0
            ? MoneyActionType.DebtWriteOff
            : requestedType;
}
