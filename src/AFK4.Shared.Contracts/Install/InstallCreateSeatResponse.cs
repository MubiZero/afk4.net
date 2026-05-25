namespace AFK4.Shared.Contracts.Install;

public sealed record InstallCreateSeatResponse(
    Guid OrganizationId,
    Guid BranchId,
    Guid ZoneId,
    Guid SeatId,
    string Name,
    int SortOrder);
