namespace AFK4.Shared.Contracts.Layout;

public sealed record SeatDto(
    Guid SeatId,
    Guid OrganizationId,
    Guid BranchId,
    Guid ZoneId,
    string Name,
    int SortOrder,
    DateTimeOffset CreatedAtUtc);
