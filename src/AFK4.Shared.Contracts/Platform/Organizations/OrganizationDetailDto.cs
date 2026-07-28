namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record OrganizationDetailDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string Status,
    string? StatusReason,
    DateTimeOffset? StatusChangedAtUtc,
    string PlanCode,
    string SubscriptionStatus,
    OrganizationLimitsDto Limits,
    IReadOnlyList<OrganizationBranchDto> Branches,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
