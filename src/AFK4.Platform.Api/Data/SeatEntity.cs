namespace AFK4.Platform.Api.Data;

public sealed class SeatEntity
{
    public Guid SeatId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ZoneId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
