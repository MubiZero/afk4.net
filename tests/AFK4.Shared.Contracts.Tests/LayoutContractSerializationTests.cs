using System.Text.Json;
using AFK4.Shared.Contracts.Layout;

namespace AFK4.Shared.Contracts.Tests;

public sealed class LayoutContractSerializationTests
{
    [Fact]
    public void UpdateZoneRequest_RoundTripsJson()
    {
        var request = new UpdateZoneRequest(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            "VIP Hall",
            30);

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<UpdateZoneRequest>(json);

        Assert.Equal(request, deserialized);
    }

    [Fact]
    public void UpdateSeatRequest_RoundTripsJson()
    {
        var request = new UpdateSeatRequest(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            "VIP-01",
            40);

        var json = JsonSerializer.Serialize(request);
        var deserialized = JsonSerializer.Deserialize<UpdateSeatRequest>(json);

        Assert.Equal(request, deserialized);
    }
}
