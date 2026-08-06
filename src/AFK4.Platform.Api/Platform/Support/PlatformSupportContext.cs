namespace AFK4.Platform.Api.Platform.Support;

public sealed record PlatformSupportContext(
    Guid GrantId,
    Guid PlatformAdminUserId,
    Guid OrganizationId,
    string Reason,
    string Permission,
    DateTimeOffset ExpiresAtUtc,
    // The grant header value that authenticated this request — not a new secret, the caller already
    // holds it (they sent it). Carried so GET /api/support-access/session can echo the same
    // PlatformSupportSessionDto shape RedeemTicketAsync returns, without minting a second token.
    string SessionToken);
