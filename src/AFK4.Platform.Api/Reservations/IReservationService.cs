using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Reservations;

public interface IReservationService
{
    Task<ReservationServiceResult<ReservationDto>> CreateOnlineAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Бронь на компанию из приложения: несколько мест на одно время одним действием, под одним
    /// идентификатором группы. Всё-или-ничего — денег не хватает на всю компанию, значит не
    /// бронируется ни одно место: посадить половину компании хуже, чем честно отказать.
    /// </summary>
    Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> CreateOnlineGroupAsync(
        Guid playerAccountId,
        Guid organizationId,
        Guid branchId,
        CreatePlayerReservationGroupRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> CancelOnlineAsync(
        Guid reservationId,
        Guid playerAccountId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Перенос собственной брони игроком: другое время, при желании другое место, та же
    /// длительность.
    ///
    /// До этого перенести бронь можно было только отменой и повторной: место при этом уходит в
    /// общий доступ на те секунды, что человек ищет новое время, а замороженная предоплата
    /// возвращается и замораживается заново — и если за эти секунды денег на кошельке не осталось,
    /// второй брони уже не будет.
    ///
    /// Цена пересчитывается: тариф зависит от времени суток, и перенос вечерней брони на утро
    /// меняет сумму. Старая заморозка снимается, новая ставится на новую сумму.
    /// </summary>
    Task<ReservationServiceResult<ReservationDto>> MoveOnlineAsync(
        Guid reservationId,
        Guid playerAccountId,
        DateTimeOffset startsAtUtc,
        Guid? seatId,
        int? expectedVersion,
        CancellationToken cancellationToken);

    /// <summary>
    /// Отмена всей компании разом. Отменять по одному месту тоже можно обычной отменой — это для
    /// случая «идти передумали все», когда четыре отдельных отмены выглядят как четыре шанса
    /// оборваться на полпути и оставить часть денег замороженной.
    /// </summary>
    Task<ReservationServiceResult<IReadOnlyList<ReservationDto>>> CancelOnlineGroupAsync(
        Guid reservationGroupId,
        Guid playerAccountId,
        CancellationToken cancellationToken);


    Task<ReservationSearchResultDto> SearchAsync(
        Guid organizationId,
        Guid branchId,
        ReservationSearchQuery query,
        CancellationToken cancellationToken);

    /// <summary>
    /// Места, на которые бронь в окне [<paramref name="startsAtUtc"/>, <paramref name="endsAtUtc"/>)
    /// встанет без конфликта. Правило то же, по которому создание и перенос брони её принимают:
    /// список, посчитанный иначе, обещал бы то, что сервер отклонит, или прятал бы то, что примет.
    /// <paramref name="excludedReservationId"/> — сама переносимая бронь: своё место она не занимает.
    /// </summary>
    Task<ReservationSeatAvailabilityDto> FindFreeSeatsAsync(
        Guid organizationId,
        Guid branchId,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        Guid? excludedReservationId,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> CreateAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateReservationRequest request,
        CancellationToken cancellationToken);

    Task<CreateReservationGroupResult> CreateGroupAsync(
        Guid branchId,
        Guid actorStaffUserId,
        CreateReservationGroupRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> UpdateAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        UpdateReservationRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> ConfirmAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        ConfirmReservationRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> SeatAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        SeatReservationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Клуб отказывает в заявке и говорит почему. Не отмена: игрок ничего не отменял, деньги ему
    /// возвращаются целиком при любых настройках филиала, и в его сетевые числа отказ не идёт.
    /// </summary>
    Task<ReservationServiceResult<ReservationDto>> RejectAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        RejectReservationRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Администратор говорит «он не приехал». Таймер ждёт положенных филиалу минут и знает только
    /// про брони с замороженными деньгами; человек за стойкой видит пустое место раньше и знает
    /// про бронь без предоплаты то, чего таймер не знает вовсе.
    /// </summary>
    Task<ReservationServiceResult<ReservationDto>> MarkNoShowAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        MarkReservationNoShowRequest request,
        CancellationToken cancellationToken);

    Task<ReservationServiceResult<ReservationDto>> CancelAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        CancelReservationRequest request,
        CancellationToken cancellationToken);
}
