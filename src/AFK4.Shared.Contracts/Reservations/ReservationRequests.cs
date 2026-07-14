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
    string? Note,
    int ExpectedVersion);

public sealed record ConfirmReservationRequest(
    Guid OrganizationId,
    int ExpectedVersion);

public sealed record SeatReservationRequest(
    Guid OrganizationId,
    int ExpectedVersion);

public sealed record CancelReservationRequest(
    Guid OrganizationId,
    string Reason,
    int ExpectedVersion);
