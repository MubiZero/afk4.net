using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// До какого момента подписанная аренда сеанса позволяет ПК оставаться открытым без связи.
///
/// Фиксированный сеанс оплачен до <c>EndsAtUtc</c>, и обрыв связи не должен отнимать у игрока
/// оплаченные минуты: аренда идёт до оплаченного конца, а не на 15 минут. Предел нужен на случай
/// очень длинных сеансов — аренда не подписывается на сутки вперёд, агент обновляет её по ходу.
/// Открытый счёт и пауза оплаченного конца не имеют — для них остаётся льготное окно.
/// </summary>
public static class SessionLeaseTerm
{
    public static readonly TimeSpan MaxPaidLease = TimeSpan.FromHours(4);

    public static DateTimeOffset ExpiresAtUtc(
        DateTimeOffset now,
        DateTimeOffset? sessionEndsAtUtc,
        string sessionState,
        int graceMinutes)
    {
        var grace = now.AddMinutes(graceMinutes);
        if (sessionEndsAtUtc is not { } endsAtUtc
            || endsAtUtc <= now
            || sessionState != SessionStateNames.Active)
        {
            return grace;
        }

        return endsAtUtc < now + MaxPaidLease ? endsAtUtc : now + MaxPaidLease;
    }
}
