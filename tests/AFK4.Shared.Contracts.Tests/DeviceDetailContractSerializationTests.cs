using System.Text.Json;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceDetailContractSerializationTests
{
    [Fact]
    public void DeviceDetailDto_RoundTripsThroughJson()
    {
        var command = new DeviceCommandStatusDto(
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            CommandId: Guid.Parse("63d6536d-f2c5-4379-a8b3-cd487f0c1e94"),
            Type: "lock",
            Status: "Pending",
            Message: null,
            CreatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:05:00Z"),
            UpdatedAtUtc: DateTimeOffset.Parse("2026-05-13T10:05:00Z"));
        var detail = new DeviceDetailDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            AgentVersion: "0.1.1",
            ShellVersion: "0.1.2",
            EnrolledAtUtc: DateTimeOffset.Parse("2026-05-13T09:00:00Z"),
            LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-13T10:00:00Z"),
            IsOnline: true,
            IsLocked: true,
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
            SeatName: "Seat 01",
            ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ZoneName: "Main Hall",
            ActiveCredentialCount: 1,
            InstalledAppCount: 2,
            RecentCommands: [command]);

        var json = JsonSerializer.Serialize(detail);
        var copy = JsonSerializer.Deserialize<DeviceDetailDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(detail.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("Seat 01", copy.SeatName);
        Assert.Equal("Main Hall", copy.ZoneName);
        Assert.Equal(1, copy.ActiveCredentialCount);
        Assert.Equal(2, copy.InstalledAppCount);
        var copiedCommand = Assert.Single(copy.RecentCommands);
        Assert.Equal(command.CommandId, copiedCommand.CommandId);
        Assert.Equal("Pending", copiedCommand.Status);
    }
}
