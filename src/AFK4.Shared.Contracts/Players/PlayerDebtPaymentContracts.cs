using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

// Гашение долга игроком с собственного кошелька. Сумма приходит явно, а не «весь долг»: человек
// вправе закрыть часть, а «весь» на момент нажатия и на момент записи — это разные числа.
public sealed record PlayerDebtPaymentRequest(MoneyDto Amount, string IdempotencyKey);
