using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.Tests;

public sealed class FloorMapWorkspaceViewModelTests
{
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");

    [Fact]
    public async Task LoadAsync_ReplacesStaticSeatsWithBackendFloorMap()
    {
        var apiClient = new RecordingFloorMapApiClient(new FloorMapDto(
            BranchId,
            "Demo Branch",
            [Seat("PC-010", "Active", DeviceId, remainingSeconds: 1800)]));
        var viewModel = new FloorMapWorkspaceViewModel(apiClient);

        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.Equal("Demo Branch", viewModel.BranchName);
        Assert.Single(viewModel.Seats);
        Assert.Equal("PC-010", viewModel.Seats[0].Name);
        Assert.Equal("Active", viewModel.Seats[0].State);
        Assert.Equal(1800, viewModel.Seats[0].RemainingSeconds);
        Assert.Equal(DeviceId, viewModel.Seats[0].DeviceId);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshCommand_ReloadsLastBranch()
    {
        var apiClient = new RecordingFloorMapApiClient(new FloorMapDto(
            BranchId,
            "Demo Branch",
            [Seat("PC-011", "Free", DeviceId, remainingSeconds: null)]));
        var viewModel = new FloorMapWorkspaceViewModel(apiClient);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        viewModel.RefreshCommand.Execute(null);

        Assert.Equal(2, apiClient.LoadCallCount);
    }

    [Fact]
    public async Task ApplyDeviceStatus_UpdatesLoadedSeatByDeviceId()
    {
        var apiClient = new RecordingFloorMapApiClient(new FloorMapDto(
            BranchId,
            "Demo Branch",
            [Seat("PC-010", "Locked", DeviceId, remainingSeconds: null)]));
        var viewModel = new FloorMapWorkspaceViewModel(apiClient);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        var updated = viewModel.ApplyDeviceStatus(new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: BranchId,
            DeviceId: DeviceId,
            MachineName: "renamed-pc",
            IsOnline: true,
            IsLocked: false,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z")));

        Assert.True(updated);
        Assert.Equal("Free", viewModel.Seats[0].State);
        Assert.Equal(DateTimeOffset.Parse("2026-05-14T10:00:00Z"), viewModel.Seats[0].LastHeartbeatAtUtc);
    }

    private static SeatStatusDto Seat(string name, string state, Guid deviceId, int? remainingSeconds)
    {
        return new SeatStatusDto(
            SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
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
            ActiveSessionId: state == "Active" ? Guid.Parse("44444444-4444-4444-8444-444444444444") : null,
            RemainingSeconds: remainingSeconds);
    }

    private sealed class RecordingFloorMapApiClient(FloorMapDto floorMap) : IOperatorFloorMapApiClient
    {
        public int LoadCallCount { get; private set; }

        public Guid LastBranchId { get; private set; }

        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
        {
            LoadCallCount++;
            LastBranchId = branchId;
            return Task.FromResult(floorMap);
        }
    }
}
