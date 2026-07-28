namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record OrganizationSummaryDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string Status,
    string PlanCode,
    string SubscriptionStatus,
    int BranchCount,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
