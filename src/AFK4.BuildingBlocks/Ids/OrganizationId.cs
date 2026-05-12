namespace AFK4.BuildingBlocks.Ids;

public readonly record struct OrganizationId(Guid Value)
{
    public static OrganizationId New() => new(Guid.NewGuid());

    public static OrganizationId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
