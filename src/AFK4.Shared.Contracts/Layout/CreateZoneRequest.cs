namespace AFK4.Shared.Contracts.Layout;

public sealed record CreateZoneRequest(
    Guid OrganizationId,
    string Name,
    int SortOrder,
    string? HardwareSummary = null);
