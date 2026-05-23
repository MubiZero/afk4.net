namespace AFK4.Shared.Contracts.Platform.Tenants;

public sealed record TenantSummaryDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string Status,
    string PlanCode,
    string SubscriptionStatus,
    int BranchCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
