using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Reservations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Reservations;

/// <summary>
/// Сколько машин в филиале свободно в окне брони.
///
/// Бронь из приложения приходит без места: какой именно компьютер дать, решает клуб при посадке.
/// Из-за этого обычная проверка пересечений по месту для таких броней всегда молчит, и зал на
/// десять машин принимал сколько угодно броней на один и тот же вечер. Разбираться приходилось
/// оператору при живых людях у стойки.
///
/// Здесь считается не «занято ли конкретное место», а «сколько мест вообще есть и сколько из них
/// уже обещано» — единственная форма проверки, которая работает для брони без места.
/// </summary>
internal static class BranchCapacity
{
    /// <summary>Машинный код отказа: свободных машин на это время не осталось.</summary>
    public const string NoSeatsAvailableCode = "no_seats_available";

    private static readonly string[] ActiveReservationStates =
    [
        ReservationStateNames.Pending,
        ReservationStateNames.Confirmed
    ];

    private static readonly string[] BlockingSessionStates =
    [
        SessionStateNames.Active,
        SessionStateNames.Paused,
        SessionStateNames.Ending
    ];

    /// <summary>
    /// Хватит ли в филиале машин, чтобы принять ещё <paramref name="requestedSeats"/> мест на окно
    /// [<paramref name="startsAtUtc"/>, <paramref name="endsAtUtc"/>).
    /// </summary>
    public static async Task<bool> HasRoomForAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        int requestedSeats,
        CancellationToken cancellationToken)
    {
        var bookableSeatIds = await LoadBookableSeatIdsAsync(
            dbContext, organizationId, branchId, cancellationToken);

        // Зал без принятых игровых машин ничего не обещает: это не «мест нет», а филиал, который
        // ещё не настроили. Ронять там бронь значит объяснять игроку чужую недонастройку.
        if (bookableSeatIds.Count == 0)
        {
            return true;
        }

        var occupied = await CountOccupiedAsync(
            dbContext, organizationId, branchId, bookableSeatIds, startsAtUtc, endsAtUtc, cancellationToken);

        return occupied + requestedSeats <= bookableSeatIds.Count;
    }

    /// <summary>
    /// Места зала, которые в принципе можно занять: место с привязанным принятым игровым
    /// компьютером.
    ///
    /// Выключенные машины считаются наравне с включёнными. Ночью и до открытия в зале не горит
    /// ничего, и вместимость «по онлайну» обнулила бы завтрашний вечер целиком — а бронируют как
    /// раз его.
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> LoadBookableSeatIdsAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken)
    {
        var seatIds = await (
            from assignment in dbContext.DeviceSeatAssignments.AsNoTracking()
            join device in dbContext.Devices.AsNoTracking() on assignment.DeviceId equals device.DeviceId
            where assignment.OrganizationId == organizationId &&
                  assignment.BranchId == branchId &&
                  assignment.DetachedAtUtc == null &&
                  device.EnrollmentState == DeviceEnrollmentStateNames.Approved &&
                  device.Role == DeviceRoleNames.GamingPc
            select assignment.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return seatIds.ToHashSet();
    }

    /// <summary>
    /// Сколько машин из <paramref name="bookableSeatIds"/> уже обещано на это окно.
    ///
    /// Бронь без места считается за одну машину: обещание «приходите вчетвером» стоит клубу четыре
    /// машины, даже когда номера машин ещё не выбраны. Бронь с местом и сессия на том же месте
    /// считаются один раз — иначе один человек занимал бы две машины и зал закрывался бы раньше,
    /// чем кончались компьютеры. Посаженные брони не считаются вовсе: их место уже держит сессия.
    ///
    /// Сессия без запланированного конца в будущее не переносится. Она длится «пока играет», и
    /// счесть сегодняшний полный зал занятым и завтра значит запретить бронировать вообще.
    /// Обратная сторона честная: бронь на ближайший час может встать рядом с открытой сессией.
    /// </summary>
    public static async Task<int> CountOccupiedAsync(
        PlatformDbContext dbContext,
        Guid organizationId,
        Guid branchId,
        IReadOnlySet<Guid> bookableSeatIds,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        CancellationToken cancellationToken)
    {
        var overlappingReservations = dbContext.Reservations
            .AsNoTracking()
            .Where(reservation =>
                reservation.OrganizationId == organizationId &&
                reservation.BranchId == branchId &&
                ActiveReservationStates.Contains(reservation.State) &&
                reservation.StartsAtUtc < endsAtUtc &&
                reservation.EndsAtUtc > startsAtUtc);

        var withoutSeat = await overlappingReservations
            .CountAsync(reservation => reservation.SeatId == null, cancellationToken);

        var reservedSeatIds = await overlappingReservations
            .Where(reservation => reservation.SeatId != null)
            .Select(reservation => reservation.SeatId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        var busySeatIds = await dbContext.Sessions
            .AsNoTracking()
            .Where(session =>
                session.OrganizationId == organizationId &&
                session.BranchId == branchId &&
                BlockingSessionStates.Contains(session.State) &&
                session.EndsAtUtc != null &&
                session.EndsAtUtc > startsAtUtc &&
                session.RequestedAtUtc < endsAtUtc)
            .Select(session => session.SeatId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Место, снятое с обслуживания вместе с машиной, не входит в вместимость — и занимать её
        // не должно: иначе зал «переполнен» компьютерами, которых в нём уже нет.
        var occupiedSeats = reservedSeatIds
            .Concat(busySeatIds)
            .Distinct()
            .Count(bookableSeatIds.Contains);

        return withoutSeat + occupiedSeats;
    }
}
