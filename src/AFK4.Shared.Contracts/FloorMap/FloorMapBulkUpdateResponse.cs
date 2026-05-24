namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapBulkUpdateResponse(
    string ETag,
    IReadOnlyList<FloorMapBulkZoneAssignment> Zones,
    IReadOnlyList<FloorMapBulkSeatAssignment> Seats);

public sealed record FloorMapBulkZoneAssignment(
    string ClientId,
    Guid ZoneId);

public sealed record FloorMapBulkSeatAssignment(
    string ClientId,
    Guid SeatId);
