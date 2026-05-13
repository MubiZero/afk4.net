using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.Tests;

public sealed class DeviceStatusStoreTests
{
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f");

    [Fact]
    public void Apply_UpdatesSeatStateByDeviceIdBeforeMachineName()
    {
        var seats = new List<FloorMapSeatViewModel>
        {
            FloorMapSeatViewModel.FromDto(Seat("PC-001", Guid.Parse("11111111-1111-4111-8111-111111111111"), "Locked")),
            FloorMapSeatViewModel.FromDto(Seat("PC-002", DeviceId, "Locked"))
        };
        var store = new DeviceStatusStore(seats);
        var status = Status(DeviceId, machineName: "PC-001", isOnline: true, isLocked: false);

        var updated = store.Apply(status);

        Assert.True(updated);
        Assert.Equal("Locked", seats[0].State);
        Assert.Equal("Free", seats[1].State);
        Assert.True(seats[1].IsOnline);
    }

    [Fact]
    public void Apply_FallsBackToMachineNameWhenDeviceIdIsUnknown()
    {
        var seats = new List<FloorMapSeatViewModel>
        {
            FloorMapSeatViewModel.FromDto(Seat("PC-002", deviceId: null, "Locked"))
        };
        var store = new DeviceStatusStore(seats);
        var status = Status(DeviceId, machineName: "PC-002", isOnline: true, isLocked: false);

        var updated = store.Apply(status);

        Assert.True(updated);
        Assert.Equal("Free", seats[0].State);
    }

    [Fact]
    public void Apply_MarksOfflineDeviceAsOffline()
    {
        var seats = new List<FloorMapSeatViewModel>
        {
            FloorMapSeatViewModel.FromDto(Seat("PC-001", DeviceId, "Locked"))
        };
        var store = new DeviceStatusStore(seats);
        var status = Status(DeviceId, machineName: "PC-001", isOnline: false, isLocked: true);

        var updated = store.Apply(status);

        Assert.True(updated);
        Assert.Equal("Offline", seats[0].State);
        Assert.False(seats[0].IsOnline);
    }

    [Fact]
    public void Apply_ReturnsFalseForUnknownDeviceAndMachine()
    {
        var seats = new List<FloorMapSeatViewModel>
        {
            FloorMapSeatViewModel.FromDto(Seat("PC-001", deviceId: null, "Locked"))
        };
        var store = new DeviceStatusStore(seats);
        var status = Status(DeviceId, machineName: "PC-999", isOnline: true, isLocked: false);

        var updated = store.Apply(status);

        Assert.False(updated);
    }

    private static DeviceStatusChangedDto Status(Guid deviceId, string machineName, bool isOnline, bool isLocked)
    {
        return new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: BranchId,
            DeviceId: deviceId,
            MachineName: machineName,
            IsOnline: isOnline,
            IsLocked: isLocked,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"));
    }

    private static SeatStatusDto Seat(string name, Guid? deviceId, string state)
    {
        return new SeatStatusDto(
            SeatId: Guid.NewGuid(),
            SeatName: name,
            ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
            ZoneName: "Main Hall",
            SortOrder: 1,
            State: state,
            DeviceId: deviceId,
            DeviceName: name,
            IsDeviceOnline: true,
            IsDeviceLocked: state == "Locked",
            LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
            AgentVersion: "0.1.1",
            ShellVersion: "0.1.2",
            ActiveSessionId: null,
            RemainingSeconds: null);
    }
}
