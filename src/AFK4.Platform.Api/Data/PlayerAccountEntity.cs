namespace AFK4.Platform.Api.Data;

public sealed class PlayerAccountEntity
{
    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid HomeBranchId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    /// <summary>Optional contact email for player notifications (email-first OTP, dunning, digests).</summary>
    public string? Email { get; set; }

    /// <summary>Preferred notification locale; null falls back to the branch/default locale at resolution.</summary>
    public string? PreferredLocale { get; set; }

    /// <summary>Player consent to marketing messages. Defaults false; toggled from the portal profile.</summary>
    public bool MarketingOptIn { get; set; }

    public bool IsActive { get; set; } = true;

    // Optional per-player override of the branch postpaid credit limit; null falls
    // back to the branch default (and null there means unbounded).
    public long? PostpaidCreditLimitMinorUnits { get; set; }

    /// <summary>
    /// Код приглашения, который игрок называет друзьям. Заводится при первом обращении к экрану
    /// «Приведи друга», а не всем подряд: у клуба тысячи заведённых на стойке аккаунтов, и
    /// большинство приложение не откроет.
    /// </summary>
    public string? ReferralCode { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
