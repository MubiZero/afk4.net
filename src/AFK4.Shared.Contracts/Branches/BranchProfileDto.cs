namespace AFK4.Shared.Contracts.Branches;

public sealed record BranchProfileDto(
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    string City,
    DateTimeOffset CreatedAtUtc);
