namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<SeatStatusDto> Seats)
{
    public IReadOnlyList<FloorMapZoneDto> Zones { get; init; } = [];
}

public sealed record FloorMapZoneDto(
    Guid ZoneId,
    string Name,
    int SortOrder);
