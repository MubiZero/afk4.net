using AFK4.Platform.Api.AntiFraud;
using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Tests.AntiFraud;

public sealed class MoneyActionGuardTests
{
    private static MoneyActionPolicy Policy(long threshold, long? perTxnCap = null, long? dailyCap = null) =>
        new(ApprovalThresholdMinorUnits: threshold, PerTransactionCapMinorUnits: perTxnCap, DailyCapMinorUnits: dailyCap);

    // --- Approval threshold (D3) ---

    [Fact]
    public void Evaluate_BelowThreshold_ExecutesNow() =>
        Assert.Equal(
            MoneyActionDecision.ExecuteNow,
            MoneyActionGuard.Evaluate(amountMinorUnits: 4000, spentTodayMinorUnits: 0, Policy(threshold: 5000)));

    [Fact]
    public void Evaluate_ExactlyAtThreshold_ExecutesNow() =>
        // Boundary: amount == threshold is still "≤ threshold" ⇒ execute (no approval).
        Assert.Equal(
            MoneyActionDecision.ExecuteNow,
            MoneyActionGuard.Evaluate(amountMinorUnits: 5000, spentTodayMinorUnits: 0, Policy(threshold: 5000)));

    [Fact]
    public void Evaluate_AboveThreshold_RequiresApproval() =>
        Assert.Equal(
            MoneyActionDecision.RequireApproval,
            MoneyActionGuard.Evaluate(amountMinorUnits: 5001, spentTodayMinorUnits: 0, Policy(threshold: 5000)));

    // --- Daily cap (D5) ⇒ Reject ---

    [Fact]
    public void Evaluate_WouldBreachDailyCap_Rejects() =>
        Assert.Equal(
            MoneyActionDecision.Reject,
            MoneyActionGuard.Evaluate(amountMinorUnits: 3000, spentTodayMinorUnits: 18000, Policy(threshold: 5000, dailyCap: 20000)));

    [Fact]
    public void Evaluate_ExactlyAtDailyCap_ExecutesNow() =>
        // Boundary: spent + amount == cap is within the cap ⇒ allowed.
        Assert.Equal(
            MoneyActionDecision.ExecuteNow,
            MoneyActionGuard.Evaluate(amountMinorUnits: 3000, spentTodayMinorUnits: 17000, Policy(threshold: 5000, dailyCap: 20000)));

    [Fact]
    public void Evaluate_DailyCapBreach_TakesPrecedenceOverApproval() =>
        // Over threshold AND would breach cap: the cap is a hard wall (§5.1) ⇒ Reject, not RequireApproval.
        Assert.Equal(
            MoneyActionDecision.Reject,
            MoneyActionGuard.Evaluate(amountMinorUnits: 6000, spentTodayMinorUnits: 18000, Policy(threshold: 5000, dailyCap: 20000)));

    // --- Per-transaction cap (D4) ⇒ Reject ---

    [Fact]
    public void Evaluate_AbovePerTransactionCap_Rejects() =>
        Assert.Equal(
            MoneyActionDecision.Reject,
            MoneyActionGuard.Evaluate(amountMinorUnits: 10001, spentTodayMinorUnits: 0, Policy(threshold: 5000, perTxnCap: 10000)));

    [Fact]
    public void Evaluate_ExactlyAtPerTransactionCap_DoesNotReject() =>
        // amount == per-txn cap is allowed; over threshold ⇒ approval (not reject).
        Assert.Equal(
            MoneyActionDecision.RequireApproval,
            MoneyActionGuard.Evaluate(amountMinorUnits: 10000, spentTodayMinorUnits: 0, Policy(threshold: 5000, perTxnCap: 10000)));

    // --- Uncapped roles (D4: managers/owners) ⇒ caps null, only threshold applies ---

    [Fact]
    public void Evaluate_NullCaps_NeverRejectsOnCap() =>
        Assert.Equal(
            MoneyActionDecision.RequireApproval,
            MoneyActionGuard.Evaluate(amountMinorUnits: 999_999, spentTodayMinorUnits: 999_999, Policy(threshold: 5000)));

    // --- Write-off classification (debt-reducing correction) ---

    [Fact]
    public void Classify_DebtReducingCorrection_IsWriteOff() =>
        Assert.Equal(
            MoneyActionType.DebtWriteOff,
            MoneyActionGuard.Classify(MoneyActionType.ManualCorrection, LedgerAccountTypeNames.Debt, signedAmountMinorUnits: -5000));

    [Fact]
    public void Classify_DebtIncreasingCorrection_StaysCorrection() =>
        Assert.Equal(
            MoneyActionType.ManualCorrection,
            MoneyActionGuard.Classify(MoneyActionType.ManualCorrection, LedgerAccountTypeNames.Debt, signedAmountMinorUnits: 5000));

    [Fact]
    public void Classify_WalletCorrection_StaysCorrection() =>
        Assert.Equal(
            MoneyActionType.ManualCorrection,
            MoneyActionGuard.Classify(MoneyActionType.ManualCorrection, LedgerAccountTypeNames.Wallet, signedAmountMinorUnits: -5000));

    [Fact]
    public void Classify_Refund_StaysRefund() =>
        Assert.Equal(
            MoneyActionType.Refund,
            MoneyActionGuard.Classify(MoneyActionType.Refund, LedgerAccountTypeNames.Wallet, signedAmountMinorUnits: -5000));
}
