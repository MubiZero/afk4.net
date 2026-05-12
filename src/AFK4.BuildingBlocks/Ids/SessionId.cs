namespace AFK4.BuildingBlocks.Ids;

public readonly record struct SessionId(Guid Value)
{
    public static SessionId New() => new(Guid.NewGuid());

    public static SessionId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
