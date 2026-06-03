namespace AFK4.Platform.Api.Data;

/// <summary>
/// A single-use, expiring self-service password-reset token for a staff/owner account. Mirrors the
/// staff access/refresh token storage shape: the plaintext token is returned once (emailed) and only
/// its SHA-256 hash is persisted. <see cref="ConsumedAtUtc"/> marks it spent so a token works once.
/// </summary>
public sealed class PasswordResetTokenEntity
{
    public Guid PasswordResetTokenId { get; set; }

    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
