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
    UnknownRole
}
