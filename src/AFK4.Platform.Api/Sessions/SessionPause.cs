using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Всё про паузу сессии в одном месте: сколько она уже стоит, сколько времени считать к оплате и
/// не пора ли закрыть её саму.
///
/// Чистые функции, и это важно: время к оплате читают и чекаут, и живая сумма на карте, и
/// автозащита по долгу. Две копии этой арифметики разошлись бы, и разошлись бы в деньгах.
/// </summary>
public static class SessionPause
{
    /// <summary>Умолчание платформы, когда филиал своего не поставил.</summary>
    public static readonly TimeSpan DefaultMaxPause = TimeSpan.FromMinutes(20);

    public static TimeSpan ResolveMaxPause(int? branchMaxPauseMinutes) =>
        branchMaxPauseMinutes is { } minutes && minutes > 0
            ? TimeSpan.FromMinutes(minutes)
            : DefaultMaxPause;

    /// <summary>Сколько сессия простояла на паузах всего, вместе с текущей незакрытой.</summary>
    public static TimeSpan PausedTotal(SessionEntity session, DateTimeOffset nowUtc)
    {
        var closed = TimeSpan.FromSeconds(Math.Max(0, session.TotalPausedSeconds));
        if (session.PausedAtUtc is not { } pausedAt)
        {
            return closed;
        }

        // Часы на сервере могут пойти назад (перевод времени, синхронизация): отрицательная пауза
        // превратилась бы в подарок клубу за счёт игрока или наоборот.
        var current = nowUtc > pausedAt ? nowUtc - pausedAt : TimeSpan.Zero;
        return closed + current;
    }

    /// <summary>
    /// Время, за которое игрок платит: от старта до now за вычетом пауз. Никогда не отрицательное.
    /// </summary>
    public static TimeSpan BillableElapsed(SessionEntity session, DateTimeOffset startedAtUtc, DateTimeOffset nowUtc)
    {
        var elapsed = nowUtc - startedAtUtc - PausedTotal(session, nowUtc);
        return elapsed > TimeSpan.Zero ? elapsed : TimeSpan.Zero;
    }

    /// <summary>Пауза стоит дольше разрешённого — место пора освобождать.</summary>
    public static bool IsPauseExpired(SessionEntity session, DateTimeOffset nowUtc, TimeSpan maxPause) =>
        session.PausedAtUtc is { } pausedAt && nowUtc - pausedAt >= maxPause;
}
