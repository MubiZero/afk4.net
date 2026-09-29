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

    /// <summary>
    /// Шаг TOTP (30 с), которым вошли в последний раз. Код того же или более раннего шага второй
    /// раз не принимается: подсмотренный через плечо код больше не открывает вторую сессию.
    /// </summary>
    public long? LastTotpStep { get; set; }

    /// <summary>
    /// Неудачные попытки пароля подряд и запрет до этого времени.
    ///
    /// Второй фактор запирался после пяти попыток, а пароль — нет: подобрать его можно было
    /// сколько угодно, и это была единственная дверь во всей системе без такой защиты. За ней —
    /// заведение клубов, деньги и права.
    /// </summary>
    public int FailedPasswordAttempts { get; set; }

    public DateTimeOffset? PasswordLockedUntilUtc { get; set; }

    public DateTimeOffset? LastSignInAtUtc { get; set; }
}
