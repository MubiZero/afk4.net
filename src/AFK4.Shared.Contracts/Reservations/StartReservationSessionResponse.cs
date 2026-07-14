using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Shared.Contracts.Reservations;

public sealed record StartReservationSessionResponse(
    ReservationDto Reservation,
    SessionCommandResponse Session);
