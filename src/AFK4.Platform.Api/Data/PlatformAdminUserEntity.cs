namespace AFK4.Platform.Api.Data;

public sealed class PlatformAdminUserEntity
{
    public Guid PlatformAdminUserId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string RolesJson { get; set; } = "[]";

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public string? TotpSecretEncrypted { get; set; }

    public DateTimeOffset? TotpEnabledAtUtc { get; set; }

    public string RecoveryCodeHashesJson { get; set; } = "[]";

    public int FailedTwoFactorAttempts { get; set; }

    public DateTimeOffset? TwoFactorLockedUntilUtc { get; set; }

    public DateTimeOffset? LastSignInAtUtc { get; set; }
}
