using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceCommandEndpointTests
{
    [Fact]
    public async Task PostDeviceCommand_ReturnsCommandDispatchedToDeviceGroup()
    {
        await using var factory = new WebApplicationFactory<Program>();
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task PostDeviceCommand_ReturnsBadRequestForBlankCommandType(string commandType)
    {
        await using var factory = new WebApplicationFactory<Program>();
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
        await using var factory = new WebApplicationFactory<Program>();
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
