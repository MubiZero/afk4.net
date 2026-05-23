namespace AFK4.Shared.Contracts.Layout;

public sealed record UpdateSeatRequest(
    Guid OrganizationId,
    Guid ZoneId,
    string Name,
    int SortOrder);
