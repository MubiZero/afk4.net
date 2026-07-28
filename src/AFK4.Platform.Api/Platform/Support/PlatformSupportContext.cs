namespace AFK4.Platform.Api.Platform.Support;

public sealed record PlatformSupportContext(
    Guid GrantId,
    Guid PlatformAdminUserId,
    Guid OrganizationId,
    string Reason,
    string Permission,
    DateTimeOffset ExpiresAtUtc);
