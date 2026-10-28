using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Конец оплаченного времени сдвинулся позже (продление, снятие паузы, возврат времени после отключения
/// света): автозащита должна снова предупредить за пять минут и запереть ПК в новый конец. Её отметки
/// ставятся раз на сессию, и без сброса после первого «время вышло» сервер больше не запирал бы этот ПК —
/// держала бы только аренда агента.
/// </summary>
public static class SessionDeadline
{
    public static void MovedLater(SessionEntity session, DateTimeOffset now)
    {
        if (session.EndsAtUtc is { } endsAtUtc && endsAtUtc > now)
        {
            session.AutoLockedAtUtc = null;
            session.AutoWarnedAtUtc = null;
        }
    }
}
