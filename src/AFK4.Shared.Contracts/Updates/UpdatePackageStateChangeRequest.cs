namespace AFK4.Shared.Contracts.Updates;

public sealed record UpdatePackageStateChangeRequest(
    Guid OrganizationId,
    string State,
    string Reason);
