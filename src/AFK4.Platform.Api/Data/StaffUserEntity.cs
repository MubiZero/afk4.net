namespace AFK4.Platform.Api.Data;

public sealed class StaffUserEntity
{
    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    /// <summary>Contact email for staff/owner notifications (invite, password reset). Null on legacy rows.</summary>
    public string? Email { get; set; }

    /// <summary>Staff login phone in E.164 display form (e.g. "+992937380070"). Null until verified.</summary>
    public string? Phone { get; set; }

    /// <summary>Digits-only form of <see cref="Phone"/> used as the global login key. Null until verified.</summary>
    public string? NormalizedPhone { get; set; }

    /// <summary>When the phone was confirmed by SMS OTP. Null = unverified; only verified phones may sign in.</summary>
    public DateTimeOffset? PhoneVerifiedAtUtc { get; set; }

    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Подряд идущие промахи по паролю и до какого момента вход закрыт. Те же поля и тот же
    /// порядок, что у администратора платформы: одна дверь — один механизм.
    /// </summary>
    public int FailedPasswordAttempts { get; set; }

    public DateTimeOffset? PasswordLockedUntilUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Чат с ботом AFK4.NET в Telegram, куда идут отчёты. Null — не привязан.</summary>
    public long? TelegramChatId { get; set; }

    public DateTimeOffset? TelegramLinkedAtUtc { get; set; }

    /// <summary>
    /// SHA-256 разового кода привязки из ссылки на бота и срок его жизни. Сам код хранится только в
    /// ссылке у человека: утечка базы не даёт привязать чужой чат к отчётам владельца.
    /// </summary>
    public string? TelegramLinkCodeHash { get; set; }

    public DateTimeOffset? TelegramLinkCodeExpiresAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
