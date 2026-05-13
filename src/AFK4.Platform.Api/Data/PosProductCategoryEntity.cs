namespace AFK4.Platform.Api.Data;

public sealed class PosProductCategoryEntity
{
    public Guid CategoryId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
