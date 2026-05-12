namespace AFK4.Shared.Contracts.FloorMap;

public sealed record FloorMapDto(
    Guid BranchId,
    string BranchName,
    IReadOnlyList<SeatStatusDto> Seats);
