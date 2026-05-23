namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminAccessTokenEntity
{
    public Guid PlatformAdminAccessTokenId { get; set; }

    public Guid PlatformAdminUserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
