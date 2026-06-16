namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapBulkUpdateRequest(
    Guid OrganizationId,
    IReadOnlyList<FloorMapBulkZoneRequest> Zones,
    IReadOnlyList<FloorMapBulkSeatRequest> Seats,
    IReadOnlyList<FloorMapBulkWallRequest>? Walls = null);

public sealed record FloorMapBulkZoneRequest(
    Guid? ZoneId,
    string ClientId,
    string Name,
    int SortOrder,
    int? GeoX = null,
    int? GeoY = null,
    int? GeoWidth = null,
    int? GeoHeight = null,
    string? Color = null,
    string? ZoneType = null);

public sealed record FloorMapBulkSeatRequest(
    Guid? SeatId,
    string ClientId,
    string ZoneClientId,
    string Name,
    int SortOrder,
    int? PosX = null,
    int? PosY = null,
    int Rotation = 0,
    string SeatType = "pc");

public sealed record FloorMapBulkWallRequest(
    int X1,
    int Y1,
    int X2,
    int Y2);
