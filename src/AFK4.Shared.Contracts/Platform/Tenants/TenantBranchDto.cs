namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record TenantBranchDto(
    Guid BranchId,
    string Slug,
    string Name,
    string City,
    DateTimeOffset CreatedAtUtc);
