namespace AFK4.Platform.Api.Data;

public sealed class PlatformSupportAccessGrantEntity
{
    public Guid GrantId { get; set; }
    public Guid PlatformAdminUserId { get; set; }
    public Guid OrganizationId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTimeOffset IssuedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public DateTimeOffset? RevokedAtUtc { get; set; }
}
