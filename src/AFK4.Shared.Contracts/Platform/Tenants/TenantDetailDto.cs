namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record TenantDetailDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string Status,
    string? StatusReason,
    DateTimeOffset? StatusChangedAtUtc,
    string PlanCode,
    string SubscriptionStatus,
    TenantLimitsDto Limits,
    IReadOnlyList<TenantBranchDto> Branches,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
