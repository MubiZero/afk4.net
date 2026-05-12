namespace AFK4.Platform.Api.Data;

public sealed class StaffUserEntity
{
    public Guid StaffUserId { get; set; }

    public Guid OrganizationId { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string NormalizedUserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
