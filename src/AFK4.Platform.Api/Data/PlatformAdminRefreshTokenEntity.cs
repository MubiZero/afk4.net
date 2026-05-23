namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminRefreshTokenEntity
{
    public Guid PlatformAdminRefreshTokenId { get; set; }

    public Guid PlatformAdminUserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
