namespace AFK4.Shared.Contracts.Layout;

public sealed record CreateSeatRequest(
    Guid OrganizationId,
    Guid ZoneId,
    string Name,
    int SortOrder);
