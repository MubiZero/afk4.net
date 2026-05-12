namespace AFK4.Shared.Contracts.FloorMap;

public sealed record SeatStatusDto(
    Guid SeatId,
    string SeatName,
    string ZoneName,
    string State,
    Guid? ActiveSessionId,
    int? RemainingSeconds);
