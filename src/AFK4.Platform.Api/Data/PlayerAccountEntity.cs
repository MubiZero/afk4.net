namespace AFK4.Platform.Api.Data;

public sealed class PlayerAccountEntity
{
    public Guid PlayerAccountId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid HomeBranchId { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public string? PhoneNumber { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
