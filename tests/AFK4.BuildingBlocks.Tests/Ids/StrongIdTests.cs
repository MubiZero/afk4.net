using AFK4.BuildingBlocks.Ids;

namespace AFK4.BuildingBlocks.Tests.Ids;

public sealed class StrongIdTests
{
    [Fact]
    public void OrganizationId_New_CreatesNonEmptyValue()
    {
        var id = OrganizationId.New();

        Assert.NotEqual(Guid.Empty, id.Value);
        Assert.Equal(id.Value.ToString("D"), id.ToString());
    }

    [Fact]
    public void DeviceId_From_PreservesValue()
    {
        var value = Guid.Parse("9f3adbd3-957e-4dc8-8d34-a6bfa56b9275");

        var id = DeviceId.From(value);

        Assert.Equal(value, id.Value);
        Assert.Equal("9f3adbd3-957e-4dc8-8d34-a6bfa56b9275", id.ToString());
    }
}
