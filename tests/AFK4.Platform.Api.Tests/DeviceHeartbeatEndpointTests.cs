using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AFK4.Platform.Api.Tests;

public sealed class DeviceHeartbeatEndpointTests
{
    [Fact]
    public async Task DeviceHeartbeat_ReturnsServerTimeAndInterval()
    {
        await using var factory = new WebApplicationFactory<Program>();
        using var client = factory.CreateClient();
        var deviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");
        var request = new DeviceHeartbeatRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: deviceId,
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            ObservedAtUtc: DateTimeOffset.UtcNow,
            IsLocked: true);

        var response = await client.PostAsJsonAsync($"/api/devices/{deviceId}/heartbeat", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<DeviceHeartbeatResponse>();
        Assert.NotNull(body);
        Assert.Equal(10, body.HeartbeatIntervalSeconds);
        Assert.Empty(body.Commands);
    }
}
