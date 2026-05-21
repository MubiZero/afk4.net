namespace AFK4.Platform.Api.Reservations;

public sealed record ReservationSearchQuery(
    DateTimeOffset? FromUtc,
    DateTimeOffset? ToUtc,
    string? State,
    string? Source,
    int? Limit);
