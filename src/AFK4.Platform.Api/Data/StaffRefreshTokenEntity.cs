namespace AFK4.Platform.Api.Data;

public sealed class StaffRefreshTokenEntity
{
    public Guid StaffRefreshTokenId { get; set; }

    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
