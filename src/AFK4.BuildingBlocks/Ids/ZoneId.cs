namespace AFK4.BuildingBlocks.Ids;

public readonly record struct ZoneId(Guid Value)
{
    public static ZoneId New() => new(Guid.NewGuid());

    public static ZoneId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
