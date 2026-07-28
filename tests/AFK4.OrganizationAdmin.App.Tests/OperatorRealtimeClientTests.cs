using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.OrganizationAdmin.App.Realtime;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OperatorRealtimeClientTests
{
    [Fact]
    public async Task DeviceStatusChanged_SchedulesStatusApplicationThroughDispatcher()
    {
        var viewModel = new FloorMapWorkspaceViewModel(new RecordingFloorMapApiClient(new FloorMapDto(
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            "Demo Branch",
            [Seat("PC-002", Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"), "Locked")])));
        await viewModel.LoadAsync(Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"), CancellationToken.None);
        var hubConnection = new TestOperatorHubConnection();
        var dispatcher = new RecordingUiDispatcher();
        await using var client = new OperatorRealtimeClient(viewModel, hubConnection, dispatcher);
        var status = new DeviceStatusChangedDto(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            DeviceId: Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
            MachineName: "PC-002",
            IsOnline: true,
            IsLocked: false,
            ObservedAtUtc: DateTimeOffset.Parse("2026-05-12T00:00:00Z"));

        await hubConnection.RaiseDeviceStatusChangedAsync(status);

        Assert.Equal(1, dispatcher.InvocationCount);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-002" && seat.State == "Free" && seat.IsOnline);
    }

    private static SeatStatusDto Seat(string name, Guid deviceId, string state)
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
            ActiveSessionId: null,
            RemainingSeconds: null);
    }

    private sealed class RecordingFloorMapApiClient(FloorMapDto floorMap) : IOperatorFloorMapApiClient
    {
        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
        {
            return Task.FromResult(floorMap);
        }
    }

    private sealed class TestOperatorHubConnection : IOperatorHubConnection
    {
        private Func<DeviceStatusChangedDto, Task>? handler;

        public void OnDeviceStatusChanged(Func<DeviceStatusChangedDto, Task> handler)
        {
            this.handler = handler;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task RaiseDeviceStatusChangedAsync(DeviceStatusChangedDto status)
        {
            return handler?.Invoke(status) ?? Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            return ValueTask.CompletedTask;
        }
    }

    private sealed class RecordingUiDispatcher : IUiDispatcher
    {
        public int InvocationCount { get; private set; }

        public Task InvokeAsync(Action action)
        {
            InvocationCount++;
            action();
            return Task.CompletedTask;
        }
    }
}
