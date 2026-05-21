namespace AFK4.Shared.Contracts.Branches;

public sealed record UpdateBranchProfileRequest(
    Guid OrganizationId,
    string Name,
    string City);
