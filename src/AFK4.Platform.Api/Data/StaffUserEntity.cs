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

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
