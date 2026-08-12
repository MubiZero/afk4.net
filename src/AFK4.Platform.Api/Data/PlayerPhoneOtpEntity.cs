namespace AFK4.Platform.Api.Data;

public enum PlayerPhoneOtpPurpose
{
    PhoneVerification = 0,
}

/// <summary>
/// One-time code sent to a player's phone. Mirrors <see cref="StaffPhoneOtpEntity"/>: same
/// lifetime, cooldown and attempt rules, separate table because players and staff are separate
/// identities and a shared table would make "whose code is this" a runtime question.
/// </summary>
public sealed class PlayerPhoneOtpEntity
{
    public Guid PlayerPhoneOtpId { get; set; }
    public Guid PlayerAccountId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>The pending phone in normalized (digits-only) form the code was sent to.</summary>
    public string Phone { get; set; } = string.Empty;

    public PlayerPhoneOtpPurpose Purpose { get; set; }

    /// <summary>SHA-256 hex of the 6-digit code. Never stores plaintext.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
