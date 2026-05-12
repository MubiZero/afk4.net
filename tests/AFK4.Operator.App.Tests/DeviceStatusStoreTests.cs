using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Tests;

public sealed class DeviceStatusStoreTests
{
    [Fact]
    public void Apply_UpdatesSeatStateByMachineName()
    {
        var viewModel = new MainWindowViewModel();
        var status = new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-002",
            IsOnline: true,
            IsLocked: false,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        var updated = viewModel.ApplyDeviceStatus(status);

        Assert.True(updated);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-002" && seat.State == "Free" && seat.IsOnline);
    }

    [Fact]
    public void Apply_MarksOfflineDeviceAsOffline()
    {
        var viewModel = new MainWindowViewModel();
        var status = new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-001",
            IsOnline: false,
            IsLocked: true,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        var updated = viewModel.ApplyDeviceStatus(status);

        Assert.True(updated);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-001" && seat.State == "Offline" && !seat.IsOnline);
    }
}
