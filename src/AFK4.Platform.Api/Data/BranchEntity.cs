namespace AFK4.Platform.Api.Data;

public sealed class BranchEntity
{
    public Guid BranchId { get; set; }

    public Guid OrganizationId { get; set; }

    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string City { get; set; } = string.Empty;

    public bool RequireManualDeviceApproval { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
