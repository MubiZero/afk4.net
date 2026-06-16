namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<SeatStatusDto> Seats)
{
    public IReadOnlyList<FloorMapZoneDto> Zones { get; init; } = [];

    public IReadOnlyList<FloorMapWallDto> Walls { get; init; } = [];
}

public sealed record FloorMapZoneDto(
    Guid ZoneId,
    string Name,
    int SortOrder)
{
    public int? GeoX { get; init; }

    public int? GeoY { get; init; }

    public int? GeoWidth { get; init; }

    public int? GeoHeight { get; init; }

    public string? Color { get; init; }

    public string? ZoneType { get; init; }
}

public sealed record FloorMapWallDto(
    Guid WallId,
    int X1,
    int Y1,
    int X2,
    int Y2);
