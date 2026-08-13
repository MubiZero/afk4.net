namespace AFK4.Shared.Contracts.Layout;

public sealed record ZoneDto(
    Guid ZoneId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    int SortOrder,
    DateTimeOffset CreatedAtUtc,
    string? HardwareSummary,
    IReadOnlyList<SeatDto> Seats);
