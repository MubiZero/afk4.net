namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record UpdateOrganizationStatusRequest(
    string Status,
    string Reason);
