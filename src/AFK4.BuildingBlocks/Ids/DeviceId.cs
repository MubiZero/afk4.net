namespace AFK4.BuildingBlocks.Ids;

public readonly record struct DeviceId(Guid Value)
{
    public static DeviceId New() => new(Guid.NewGuid());

    public static DeviceId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
