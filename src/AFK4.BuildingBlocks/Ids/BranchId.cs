namespace AFK4.BuildingBlocks.Ids;

public readonly record struct BranchId(Guid Value)
{
    public static BranchId New() => new(Guid.NewGuid());

    public static BranchId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
