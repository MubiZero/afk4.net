using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Players;

/// <summary>Игрок сам заканчивает свою сессию и освобождает место.</summary>
public sealed record PlayerSelfEndSessionRequest(string IdempotencyKey);

/// <summary>
/// Чем закончился ранний выход. Возврат показывается игроку явно: «я встал раньше» и «мне
/// вернули столько-то» — это одно событие, и узнавать вторую половину из истории кошелька
/// человек не должен.
/// </summary>
/// <param name="BilledMinutes">
/// Сколько минут списано. Это не фактические минуты, а тарифицируемые: минимальная
/// длительность и шаг округления тарифа уже применены, ровно как у стойки.
/// </param>
public sealed record PlayerSelfEndSessionResponse(
    int BilledMinutes,
    // Сколько вернулось на кошелёк. Ноль — значит время было отыграно полностью.
    MoneyDto Refunded,
    // Сколько минут вернулось в пакет — у сессии, начатой по пакету.
    int PackageMinutesReturned = 0);

/// <summary>
/// «Сколько вернётся, если встать сейчас» — до нажатия. Тот же расчёт, что у самого выхода: экран
/// не обещает одну сумму, чтобы вернуть другую.
/// </summary>
public sealed record PlayerEndQuoteDto(
    // Сколько минут будет списано: сыгранное за вычетом пауз, с правилами тарифа.
    int BilledMinutes,
    MoneyDto Refund,
    int PackageMinutesReturned);
