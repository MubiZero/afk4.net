namespace AFK4.Shared.Contracts.Platform.Auth;

public sealed record PlatformAdminListItem(
    Guid PlatformAdminUserId,
    string UserName,
    string DisplayName,
    string Role,
    bool IsActive,
    bool TwoFactorEnabled,
    DateTimeOffset? LastSignInAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record PlatformAdminInvitationDto(
    Guid InvitationId,
    string Role,
    string Status,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset CreatedAtUtc);

public sealed record CreatePlatformAdminInvitationRequest(string Role, int LifetimeHours);

public sealed record CreatePlatformAdminInvitationResponse(PlatformAdminInvitationDto Invitation, string Code);

public sealed record UpdatePlatformAdminRequest(string? Role, bool? IsActive);

public sealed record AcceptPlatformAdminInvitationRequest(string Code, string UserName, string DisplayName, string Password);

public enum PlatformAdminDirectoryError
{
    None,
    LastFullAdmin,
    SelfDemotion,
    NotFound,
    UnknownRole,

    // The invitation lookup failed for any reason (code doesn't exist, expired, revoked, already
    // accepted). All of those map to this single value on purpose — the anonymous acceptance
    // endpoint must not let a caller distinguish "no such code" from "code expired" by response
    // shape, or invitation codes become enumerable.
    InvalidInvitationCode,

    // The requested login is already taken by another platform admin. Distinct from
    // InvalidInvitationCode because it is not a secret-guessing signal — usernames are not secret.
    UserNameTaken,

    // Generic "another concurrent change won the race, retry" outcome — NOT a claim about which
    // business rule was violated. A serializable-transaction conflict can abort either side of a
    // race, including one whose own change had nothing to do with LastFullAdmin, so it must not be
    // reported as that specific business error.
    Conflict,

    // Requested invitation lifetime falls outside the allowed range (see
    // PlatformAdminDirectoryService.MinInvitationLifetimeHours / MaxInvitationLifetimeHours).
    // Add new values after this one; keep this last so the ordering comments above stay true.
    InvalidInvitationLifetime
}
