namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminInvitationEntity
{
    public Guid InvitationId { get; set; }

    public byte[] CodeHash { get; set; } = [];

    public string Role { get; set; } = string.Empty;

    public string Status { get; set; } = "pending";

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public Guid CreatedByPlatformAdminUserId { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? AcceptedAtUtc { get; set; }

    public Guid? AcceptedPlatformAdminUserId { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }
}
