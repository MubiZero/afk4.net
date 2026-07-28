namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record CreateTenantRequest(
    string OrganizationSlug,
    string OrganizationName,
    string BranchSlug,
    string BranchName,
    string BranchCity,
    string PlanCode,
    string SubscriptionStatus,
    TenantLimitsDto? Limits,
    string? OwnerUserName,
    string? OwnerDisplayName,
    TimeSpan? OrganizationOwnerInviteLifetime);
