namespace AFK4.Shared.Contracts.Platform.Invites;

public sealed record OwnerInviteDto(
    Guid OwnerInviteId,
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
