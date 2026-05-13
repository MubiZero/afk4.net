namespace AFK4.Shared.Contracts.Billing;

public sealed record WalletSummaryDto(
    Guid PlayerAccountId,
    MoneyDto WalletBalance,
    MoneyDto DebtBalance,
    IReadOnlyList<LedgerEntryDto> RecentEntries);
