using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerDashboardDto(
    MoneyDto WalletBalance,
    MoneyDto DebtBalance,
    ActiveSessionDto? ActiveSession);
