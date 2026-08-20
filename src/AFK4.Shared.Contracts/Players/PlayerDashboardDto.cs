using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

/// <summary>
/// Главный экран игрока: три числа кошелька и текущая сессия, если она идёт.
/// <paramref name="HeldBalance"/> — придержанное под брони; из <paramref name="WalletBalance"/> оно
/// уже вычтено, и это ответ на вопрос «а куда делись мои деньги», а не четвёртое место их хранения.
/// </summary>
public sealed record PlayerDashboardDto(
    MoneyDto WalletBalance,
    MoneyDto HeldBalance,
    MoneyDto DebtBalance,
    ActiveSessionDto? ActiveSession);
