namespace AFK4.Platform.Api.Data;

public sealed class OwnerCodeEntity
{
    public Guid OwnerCodeId { get; set; }

    public Guid StaffUserId { get; set; }

    public string CodeHash { get; set; } = string.Empty;

    public string CodeSuffix { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public int FailedAttemptCount { get; set; }

    public DateTimeOffset? RevokedAtUtc { get; set; }

    public string? RevokedReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
