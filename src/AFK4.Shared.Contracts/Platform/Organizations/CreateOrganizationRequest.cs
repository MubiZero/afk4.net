namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record CreateOrganizationRequest(
    string OrganizationSlug,
    string OrganizationName,
    string BranchSlug,
    string BranchName,
    string BranchCity,
    string PlanCode,
    string SubscriptionStatus,
    OrganizationLimitsDto? Limits,
    string? OwnerUserName,
    string? OwnerDisplayName,
    TimeSpan? OrganizationOwnerInviteLifetime);
