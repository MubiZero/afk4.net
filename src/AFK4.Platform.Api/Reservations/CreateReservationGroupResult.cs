using AFK4.Shared.Contracts.Reservations;

namespace AFK4.Platform.Api.Reservations;

public enum ReservationGroupStatus
{
    Created,
    Conflict,
    Invalid
}

public sealed record CreateReservationGroupResult(
    ReservationGroupStatus Status,
    ReservationGroupResultDto? Result,
    string? Error)
{
    public static CreateReservationGroupResult Created(ReservationGroupResultDto result) =>
        new(ReservationGroupStatus.Created, result, null);

    public static CreateReservationGroupResult Conflict(ReservationGroupResultDto result) =>
        new(ReservationGroupStatus.Conflict, result, null);

    public static CreateReservationGroupResult Invalid(string error) =>
        new(ReservationGroupStatus.Invalid, null, error);
}
