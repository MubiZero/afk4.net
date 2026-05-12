using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DispatchDeviceCommandRequestSerializationTests
{
    [Fact]
    public void DispatchDeviceCommandRequest_RoundTripsPayload()
    {
        var request = new DispatchDeviceCommandRequest(
            Type: "lock",
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "technician-workflow"
            });

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DispatchDeviceCommandRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal("lock", copy.Type);
        Assert.Equal("technician-workflow", copy.Payload["reason"]);
    }
}
