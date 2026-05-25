using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Install;

namespace AFK4.Shared.Contracts.Tests;

public sealed class DeviceDetailContractSerializationTests
{
    [Fact]
    public void DeviceInventoryItemDto_RoundTripsThroughJson()
    {
        var item = new DeviceInventoryItemDto(
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
            PendingCommandCount: 3,
            FailedCommandCount: 1,
            DisplayName: "VIP-01",
            Role: DeviceRoleNames.ManagerWorkstation,
            EnrollmentState: DeviceEnrollmentStateNames.Pending);

        var json = JsonSerializer.Serialize(item);
        var copy = JsonSerializer.Deserialize<DeviceInventoryItemDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(item.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("Seat 01", copy.SeatName);
        Assert.Equal("Main Hall", copy.ZoneName);
        Assert.Equal(1, copy.ActiveCredentialCount);
        Assert.Equal(2, copy.InstalledAppCount);
        Assert.Equal(3, copy.PendingCommandCount);
        Assert.Equal(1, copy.FailedCommandCount);
        Assert.Equal("VIP-01", copy.DisplayName);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, copy.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Pending, copy.EnrollmentState);
    }

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
            RecentCommands: [command],
            DisplayName: "VIP-01",
            Role: DeviceRoleNames.ManagerWorkstation,
            EnrollmentState: DeviceEnrollmentStateNames.Approved);

        var json = JsonSerializer.Serialize(detail);
        var copy = JsonSerializer.Deserialize<DeviceDetailDto>(json);

        Assert.NotNull(copy);
        Assert.Equal(detail.DeviceId, copy.DeviceId);
        Assert.Equal("PC-001", copy.MachineName);
        Assert.Equal("Seat 01", copy.SeatName);
        Assert.Equal("Main Hall", copy.ZoneName);
        Assert.Equal(1, copy.ActiveCredentialCount);
        Assert.Equal(2, copy.InstalledAppCount);
        Assert.Equal("VIP-01", copy.DisplayName);
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, copy.Role);
        Assert.Equal(DeviceEnrollmentStateNames.Approved, copy.EnrollmentState);
        var copiedCommand = Assert.Single(copy.RecentCommands);
        Assert.Equal(command.CommandId, copiedCommand.CommandId);
        Assert.Equal("Pending", copiedCommand.Status);
    }

    [Fact]
    public void DeviceAdminRequests_RoundTripThroughJson()
    {
        var organizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
        var seatId = Guid.Parse("11111111-1111-4111-8111-111111111111");

        var stateChange = JsonSerializer.Deserialize<DeviceStateChangeRequest>(
            JsonSerializer.Serialize(new DeviceStateChangeRequest(organizationId, "Checked at desk.")));
        var rename = JsonSerializer.Deserialize<RenameDeviceRequest>(
            JsonSerializer.Serialize(new RenameDeviceRequest(organizationId, "VIP-01")));
        var move = JsonSerializer.Deserialize<MoveDeviceSeatRequest>(
            JsonSerializer.Serialize(new MoveDeviceSeatRequest(organizationId, seatId)));

        Assert.NotNull(stateChange);
        Assert.Equal(organizationId, stateChange.OrganizationId);
        Assert.Equal("Checked at desk.", stateChange.Reason);
        Assert.NotNull(rename);
        Assert.Equal("VIP-01", rename.DisplayName);
        Assert.NotNull(move);
        Assert.Equal(seatId, move.SeatId);
    }
}
