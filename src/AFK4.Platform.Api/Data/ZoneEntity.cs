namespace AFK4.Platform.Api.Data;

public sealed class ZoneEntity
{
    public Guid ZoneId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // Floor-plan rectangle in grid cells (null until arranged).
    public int? GeoX { get; set; }

    public int? GeoY { get; set; }

    public int? GeoWidth { get; set; }

    public int? GeoHeight { get; set; }

    public string? Color { get; set; }

    public string? ZoneType { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
