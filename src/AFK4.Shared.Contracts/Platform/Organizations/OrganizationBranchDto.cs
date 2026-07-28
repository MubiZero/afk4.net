namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record OrganizationBranchDto(
    Guid BranchId,
    string Slug,
    string Name,
    string City,
    DateTimeOffset CreatedAtUtc);
