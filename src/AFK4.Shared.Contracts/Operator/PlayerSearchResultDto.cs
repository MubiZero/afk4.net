namespace AFK4.Shared.Contracts.Operator;

public sealed record PlayerSearchResultDto(
    Guid PlayerAccountId,
    string DisplayName,
    string? PhoneNumber,
    long WalletBalanceMinorUnits,
    long DebtBalanceMinorUnits,
    int ActivePackageCount,
    bool IsActive);
