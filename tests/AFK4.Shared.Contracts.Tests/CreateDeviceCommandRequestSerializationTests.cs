using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class CreateDeviceCommandRequestSerializationTests
{
    [Fact]
    public void CreateDeviceCommandRequest_RoundTripsPayload()
    {
        var request = new CreateDeviceCommandRequest(
            Type: "lock",
            Payload: new Dictionary<string, string>
            {
                ["reason"] = "technician-workflow"
            });

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<CreateDeviceCommandRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal("lock", copy.Type);
        Assert.Equal("technician-workflow", copy.Payload["reason"]);
    }
}
