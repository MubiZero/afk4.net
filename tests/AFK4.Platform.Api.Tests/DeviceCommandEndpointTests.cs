using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandEndpointTests
{
    [Fact]
    public async Task PostDeviceCommand_ReturnsCommandDispatchedToDeviceGroup()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var response = await client.PostAsJsonAsync(
            $"/api/devices/{deviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();

        Assert.NotNull(command);
        Assert.NotEqual(Guid.Empty, command.CommandId);
        Assert.Equal("lock", command.Type);
        Assert.Equal("operator-request", command.Payload["reason"]);
    }

    [Fact]
    public async Task PostDeviceCommand_PersistsPendingCommandStatus()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var response = await client.PostAsJsonAsync(
            $"/api/devices/{deviceId}/commands",
            new
            {
                Type = "lock",
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });
        var command = await response.Content.ReadFromJsonAsync<DeviceCommandDto>();

        Assert.NotNull(command);

        var statusResponse = await client.GetAsync($"/api/devices/{deviceId}/commands/{command.CommandId}/status");
        var status = await statusResponse.Content.ReadFromJsonAsync<DeviceCommandStatusDto>();

        Assert.Equal(HttpStatusCode.OK, statusResponse.StatusCode);
        Assert.NotNull(status);
        Assert.Equal(deviceId, status.DeviceId);
        Assert.Equal(command.CommandId, status.CommandId);
        Assert.Equal("lock", status.Type);
        Assert.Equal("Pending", status.Status);
        Assert.Null(status.Message);
        Assert.Equal(command.CreatedAtUtc, status.CreatedAtUtc);
        Assert.Equal(command.CreatedAtUtc, status.UpdatedAtUtc);
    }

    [Fact]
    public async Task GetDeviceCommandStatus_ReturnsNotFoundForUnknownCommand()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var commandId = Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94");

        var response = await client.GetAsync($"/api/devices/{deviceId}/commands/{commandId}/status");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostDeviceCommand_ReturnsBadRequestForBlankCommandType(string commandType)
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var response = await client.PostAsJsonAsync(
            $"/api/devices/{deviceId}/commands",
            new
            {
                Type = commandType,
                Payload = new Dictionary<string, string>
                {
                    ["reason"] = "operator-request"
                }
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostDeviceCommand_ReturnsBadRequestForMissingPayload()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();

        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var response = await client.PostAsJsonAsync(
            $"/api/devices/{deviceId}/commands",
            new
            {
                Type = "lock",
                Payload = (Dictionary<string, string>?)null
            });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
