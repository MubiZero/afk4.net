namespace AFK4.Platform.Api.Identity;

public sealed record PlayerContext(
    Guid PlayerAccountId,
    Guid OrganizationId,
    bool PhoneVerified);
