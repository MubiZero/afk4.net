namespace AFK4.Platform.Api.Data;

public sealed class SeatEntity
{
    public Guid SeatId { get; set; }

    public Guid OrganizationId { get; set; }

    public Guid BranchId { get; set; }

    public Guid ZoneId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    // Floor-plan layout (null until the branch is arranged in the «План» editor; grid view ignores these).
    public int? PosX { get; set; }

    public int? PosY { get; set; }

    public int Rotation { get; set; }

    public string SeatType { get; set; } = "pc";

    public DateTimeOffset CreatedAtUtc { get; set; }
}
