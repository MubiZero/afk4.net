namespace AFK4.Platform.Api.Data;

public enum StaffPhoneOtpPurpose
{
    PhoneVerification = 0,
    PasswordReset = 1,
}

public sealed class StaffPhoneOtpEntity
{
    public Guid StaffPhoneOtpId { get; set; }
    public Guid StaffUserId { get; set; }
    public Guid OrganizationId { get; set; }

    /// <summary>The pending phone in normalized (digits-only) form the code was sent to.</summary>
    public string Phone { get; set; } = string.Empty;

    public StaffPhoneOtpPurpose Purpose { get; set; }

    /// <summary>SHA-256 hex of the 6-digit code. Never stores plaintext.</summary>
    public string CodeHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? ConsumedAtUtc { get; set; }
}
