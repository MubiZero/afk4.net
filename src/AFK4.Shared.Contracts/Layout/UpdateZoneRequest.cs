namespace AFK4.Shared.Contracts.Layout;

public sealed record UpdateZoneRequest(
    Guid OrganizationId,
    string Name,
    int SortOrder,
    string? HardwareSummary = null);
