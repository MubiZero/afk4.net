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
