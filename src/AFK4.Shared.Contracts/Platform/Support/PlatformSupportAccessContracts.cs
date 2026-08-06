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

public sealed record PlatformSupportAccessGrantIssue(
    PlatformSupportAccessGrantDto Grant,
    string Ticket,
    string AdminUrl);

public sealed record PlatformSupportSessionDto(
    string SessionToken,
    Guid OrganizationId,
    string OrganizationName,
    string Reason,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyList<string> WritableAreas);

public sealed record RedeemSupportAccessTicketRequest(string Ticket);
