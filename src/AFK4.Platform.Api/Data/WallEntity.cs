namespace AFK4.Platform.Api.Data;

public sealed class WallEntity
{
    public Guid WallId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public int X1 { get; set; }

    public int Y1 { get; set; }

    public int X2 { get; set; }

    public int Y2 { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
}
