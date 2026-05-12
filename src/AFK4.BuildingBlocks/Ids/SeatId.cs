namespace AFK4.BuildingBlocks.Ids;

public readonly record struct SeatId(Guid Value)
{
    public static SeatId New() => new(Guid.NewGuid());

    public static SeatId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
