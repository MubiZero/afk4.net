namespace AFK4.Platform.Api.Data;

/// <summary>
/// A single-use, expiring self-service password-reset code for a staff/owner account. Only the
/// SHA-256 hash of the 6-digit code is persisted (the plaintext is emailed once).
/// <see cref="ConsumedAtUtc"/> marks it spent so a code works once; <see cref="AttemptCount"/>
/// caps wrong guesses (parity with the SMS OTP).
/// </summary>
public sealed class PasswordResetTokenEntity
{
    public Guid PasswordResetTokenId { get; set; }

    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public byte[] TokenHash { get; set; } = [];

    public int AttemptCount { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
