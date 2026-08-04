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

public enum PlatformAdminDirectoryError
{
    None,
    LastFullAdmin,
    SelfDemotion,
    NotFound,
    UnknownRole,

    // Generic "another concurrent change won the race, retry" outcome — NOT a claim about which
    // business rule was violated. A serializable-transaction conflict can abort either side of a
    // race, including one whose own change had nothing to do with LastFullAdmin, so it must not be
    // reported as that specific business error. Add new values after this one; keep this last so
    // this comment stays true.
    Conflict
}
