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

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
