using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Держит ли идущая сессия машину в окне брони — одно правило на все вопросы о будущем окне:
/// «сколько машин свободно» (<see cref="BranchCapacity"/>), «свободно ли это место» (проверка
/// конфликта при создании и переносе брони) и «какие места свободны» (список под перенос).
///
/// Раньше вопрос о конкретном месте считал бессрочную сессию занимающей машину навсегда, а
/// вопрос о вместимости — только по текущий момент. Панель предлагала под перенос завтрашней
/// брони места, свободные прямо сейчас, и честнее не становилась ни по одному из правил: место
/// с гостем без конца сессии сегодня было закрыто для брони на завтра, хотя вместимость того же
/// вечера его уже считала свободным.
/// </summary>
internal static class SeatOccupancy
{
    public static readonly string[] BlockingSessionStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    /// <summary>
    /// Сессии филиала, которые держат машину в окне [<paramref name="startsAtUtc"/>,
    /// <paramref name="endsAtUtc"/>).
    ///
    /// Сессия держит машину до своего запланированного конца; если конца нет или он уже прошёл
    /// (играют дольше оплаченного) — до текущего момента. Второе слагаемое от строки не зависит: у
    /// окна, которое уже началось, считаются все живые сессии, а в будущее бессрочная сессия не
    /// переносится — счесть сегодняшний полный зал занятым и завтра значит запретить бронировать
    /// вообще.
    /// </summary>
    public static IQueryable<SessionEntity> BlockingSessions(
        IQueryable<SessionEntity> sessions,
        Guid organizationId,
        Guid branchId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        DateTimeOffset now) =>
        sessions.Where(session =>
            session.OrganizationId == organizationId &&
            session.BranchId == branchId &&
            BlockingSessionStates.Contains(session.State) &&
            (session.EndsAtUtc > startsAtUtc || now > startsAtUtc) &&
            session.RequestedAtUtc < endsAtUtc);
}
