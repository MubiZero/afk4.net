namespace AFK4.Shared.Contracts.Identity.AccountActivation;

public sealed record OrganizationOwnerInviteDto(
    Guid OrganizationOwnerInviteId,
    Guid OrganizationId,
    Guid BranchId,
    string Code,
    string Status,
    string? OwnerUserName,
    string? OwnerDisplayName,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? AcceptedAtUtc,
    DateTimeOffset? RevokedAtUtc,
    string? RevokedReason,
    DateTimeOffset CreatedAtUtc);
