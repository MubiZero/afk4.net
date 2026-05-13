using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceRealtimeContractSerializationTests
{
    [Fact]
    public void DeviceConnectionRequest_RoundTripsThroughJson()
    {
        var request = new DeviceConnectionRequest(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.0",
            ShellVersion: "0.1.0",
            CredentialSecret: "device-secret",
            ConnectedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"),
            ActiveSessionId: null,
            ActiveSessionLeaseExpiresAtUtc: null,
            ActiveSessionLeaseSequence: null);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<DeviceConnectionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("0.1.0", copy.AgentVersion);
        Assert.Equal("device-secret", copy.CredentialSecret);
    }

    [Fact]
    public void DeviceCommandResultDto_RoundTripsThroughJson()
    {
        var result = new DeviceCommandResultDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Status: "Accepted",
            Message: "Command accepted by Agent skeleton.",
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:05Z"));

        var json = JsonSerializer.Serialize(result);
        var copy = JsonSerializer.Deserialize<DeviceCommandResultDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(result.CommandId, copy.CommandId);
        Assert.Equal("Accepted", copy.Status);
        Assert.Equal("Command accepted by Agent skeleton.", copy.Message);
    }

    [Fact]
    public void DeviceRealtimeNames_AreStable()
    {
        Assert.Equal("deviceStatusChanged", DeviceRealtimeEvents.DeviceStatusChanged);
        Assert.Equal("deviceCommand", DeviceRealtimeEvents.DeviceCommand);
        Assert.Equal("deviceCommandResult", DeviceRealtimeEvents.DeviceCommandResult);
        Assert.Equal("deviceRegistered", DeviceRealtimeEvents.DeviceRegistered);
        Assert.Equal("RegisterDeviceAsync", DeviceRealtimeMethods.RegisterDeviceAsync);
        Assert.Equal("ReportCommandResultAsync", DeviceRealtimeMethods.ReportCommandResultAsync);
    }
}
