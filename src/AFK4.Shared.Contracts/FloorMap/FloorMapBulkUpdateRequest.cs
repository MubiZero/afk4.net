namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapBulkUpdateRequest(
    Guid OrganizationId,
    IReadOnlyList<FloorMapBulkZoneRequest> Zones,
    IReadOnlyList<FloorMapBulkSeatRequest> Seats);

public sealed record FloorMapBulkZoneRequest(
    Guid? ZoneId,
    string ClientId,
    string Name,
    int SortOrder);

public sealed record FloorMapBulkSeatRequest(
    Guid? SeatId,
    string ClientId,
    string ZoneClientId,
    string Name,
    int SortOrder);
