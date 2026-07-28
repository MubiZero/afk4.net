namespace AFK4.Shared.Contracts.Platform.Support;

public sealed record CreatePlatformSupportAccessGrantRequest(
    Guid OrganizationId,
    string Reason,
    int LifetimeMinutes);

public sealed record PlatformSupportAccessGrantDto(
    Guid GrantId,
    Guid OrganizationId,
    string Reason,
    DateTimeOffset IssuedAtUtc,
    DateTimeOffset ExpiresAtUtc,
    DateTimeOffset? RevokedAtUtc);
