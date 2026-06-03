namespace AFK4.Platform.Api.Data;

public sealed class PlayerAccessTokenEntity
{
    public Guid PlayerAccessTokenId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
