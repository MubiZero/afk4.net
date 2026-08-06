namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminSignInChallengeEntity
{
    public Guid ChallengeId { get; set; }

    public Guid PlatformAdminUserId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
