using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// Read-only preview of a session checkout: the bill breakdown the operator needs
/// to enter split payments (time charge + attached POS = grand total), the billable
/// seconds for the "Наиграно" display, and — when the session has a player — the
/// wallet balance so a wallet part can be auto-suggested. No state is changed.
/// </summary>
public sealed record SessionCheckoutQuoteResponse(
    Guid SessionId,
    MoneyDto TimeCharge,
    MoneyDto PosTotal,
    MoneyDto GrandTotal,
    int BillableSeconds,
    Guid? PlayerAccountId,
    MoneyDto? WalletBalance);
