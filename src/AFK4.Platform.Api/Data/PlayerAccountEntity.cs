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

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
