namespace AFK4.Shared.Contracts.Identity.AccountActivation;

public sealed record OrganizationOwnerInviteSummaryDto(
    Guid OrganizationOwnerInviteId,
    Guid OrganizationId,
    Guid BranchId,
    string CodeSuffix,
    string Status,
    string? OwnerUserName,
    string? OwnerDisplayName,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevokedReason,
    DateTimeOffset CreatedAtUtc);
