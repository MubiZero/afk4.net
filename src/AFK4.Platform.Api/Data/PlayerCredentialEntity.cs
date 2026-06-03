namespace AFK4.Platform.Api.Data;

/// <summary>
/// Login credentials and contact-verification state for a player. 1:1 with
/// <see cref="PlayerAccountEntity"/>. Kept separate so a player can exist
/// (counter-created) before they ever claim portal/shell access. A PIN is just a
/// short numeric password stored in <see cref="PasswordHash"/>.
/// </summary>
public sealed class PlayerCredentialEntity
{
    public Guid PlayerCredentialId { get; set; }

    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Null for accounts that have not set a PIN/password yet (OTP-only future).</summary>
    public string? PasswordHash { get; set; }

    public bool PhoneVerified { get; set; }

    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }

    public int FailedLoginCount { get; set; }

    public DateTimeOffset? LockedUntilUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
