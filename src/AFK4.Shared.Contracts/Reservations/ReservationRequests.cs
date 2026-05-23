namespace AFK4.Shared.Contracts.Reservations;

public sealed record CreateReservationRequest(
    Guid OrganizationId,
    Guid? PlayerAccountId,
    Guid? SeatId,
    string CustomerName,
    string? PhoneNumber,
    DateTimeOffset StartsAtUtc,
    int DurationMinutes,
    string Source,
    string? Note);

public sealed record UpdateReservationRequest(
    Guid OrganizationId,
    Guid? PlayerAccountId,
    Guid? SeatId,
    string? CustomerName,
    string? PhoneNumber,
    DateTimeOffset? StartsAtUtc,
    int? DurationMinutes,
    string? Source,
    string? Note);

public sealed record ConfirmReservationRequest(
    Guid OrganizationId);

public sealed record SeatReservationRequest(
    Guid OrganizationId);

public sealed record CancelReservationRequest(
    Guid OrganizationId,
    string Reason);
