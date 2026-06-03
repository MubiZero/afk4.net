using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.AntiFraud;

/// <summary>
/// Ledger entry types that count toward an actor's high-risk daily spend (anti-fraud spec §5.3):
/// refunds (which write <c>refund</c>/<c>reversal</c> entries) and manual corrections (debt
/// write-offs are corrections on a debt account, already covered). Kept as a plain array so EF
/// translates <c>Contains</c> to a SQL <c>IN</c>; stored values are the canonical lowercase constants.
/// </summary>
public static class MoneyActionEntryTypes
{
    public static readonly string[] HighRisk =
    [
        LedgerEntryTypeNames.Refund,
        LedgerEntryTypeNames.Reversal,
        LedgerEntryTypeNames.ManualCorrection,
    ];
}
