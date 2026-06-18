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

    Task<ReservationServiceResult<ReservationDto>> CancelOnlineAsync(
        Guid reservationId,
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

    Task<ReservationServiceResult<ReservationDto>> CancelAsync(
        Guid reservationId,
        Guid actorStaffUserId,
        CancelReservationRequest request,
        CancellationToken cancellationToken);
}
