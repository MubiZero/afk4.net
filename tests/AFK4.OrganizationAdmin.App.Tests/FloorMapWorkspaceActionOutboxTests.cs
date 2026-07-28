using System.Net.Http;
using AFK4.OrganizationAdmin.App.Devices;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.OrganizationAdmin.App.Mvvm;
using AFK4.OrganizationAdmin.App.Sessions;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class FloorMapWorkspaceActionOutboxTests
{
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public async Task LockSeat_WhileOffline_EnqueuesToOutboxAndDoesNotDispatch()
    {
        var cache = new SeededFloorMapCache(FloorMap("Cached Branch", "Active", SessionId), Now.AddMinutes(-5));
        var device = new RecordingDeviceApiClient();
        var outbox = new InMemoryActionOutbox();
        var viewModel = CreateViewModel(new ThrowingFloorMapApiClient(), cache, device, outbox);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        viewModel.SelectedSeat = viewModel.Seats[0];

        viewModel.LockSeatCommand.Execute(null);

        Assert.Empty(device.Dispatched);
        var entry = Assert.Single(outbox.Pending);
        Assert.Equal(DeviceId, entry.DeviceId);
        Assert.Equal("lock", entry.CommandType);
        Assert.Equal(SessionId, entry.ExpectedSessionId);
        Assert.True(viewModel.Seats[0].HasQueuedAction);
    }

    [Fact]
    public async Task UnlockSeat_WhileOnline_DispatchesImmediately()
    {
        var cache = new SeededFloorMapCache();
        var device = new RecordingDeviceApiClient();
        var outbox = new InMemoryActionOutbox();
        var apiClient = new StubFloorMapApiClient(FloorMap("Live Branch", "Active", SessionId));
        var viewModel = CreateViewModel(apiClient, cache, device, outbox);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        viewModel.SelectedSeat = viewModel.Seats[0];

        viewModel.UnlockSeatCommand.Execute(null);
        await Task.Yield();

        Assert.Empty(outbox.Pending);
        var dispatched = Assert.Single(device.Dispatched);
        Assert.Equal(DeviceId, dispatched.DeviceId);
        Assert.Equal("unlock", dispatched.Type);
    }

    [Fact]
    public async Task Reconnect_ReplaysQueuedAction_WhenSessionUnchanged()
    {
        var cache = new SeededFloorMapCache(FloorMap("Cached Branch", "Active", SessionId), Now.AddMinutes(-5));
        var device = new RecordingDeviceApiClient();
        var outbox = new InMemoryActionOutbox();
        var apiClient = new ToggleFloorMapApiClient();
        var viewModel = CreateViewModel(apiClient, cache, device, outbox);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        viewModel.SelectedSeat = viewModel.Seats[0];
        viewModel.LockSeatCommand.Execute(null);

        // The platform comes back with the same session still active on the seat.
        apiClient.SetResult(FloorMap("Live Branch", "Active", SessionId));
        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.False(viewModel.IsOffline);
        Assert.Empty(outbox.Pending);
        var dispatched = Assert.Single(device.Dispatched);
        Assert.Equal("lock", dispatched.Type);
        Assert.Empty(viewModel.OfflineActionAudit);
    }

    [Fact]
    public async Task Reconnect_DropsSupersededAction_WithAuditNote()
    {
        var cache = new SeededFloorMapCache(FloorMap("Cached Branch", "Active", SessionId), Now.AddMinutes(-5));
        var device = new RecordingDeviceApiClient();
        var outbox = new InMemoryActionOutbox();
        var apiClient = new ToggleFloorMapApiClient();
        var viewModel = CreateViewModel(apiClient, cache, device, outbox);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        viewModel.SelectedSeat = viewModel.Seats[0];
        viewModel.UnlockSeatCommand.Execute(null);

        // The session was force-ended cloud-side; the seat is now locked with no active session.
        apiClient.SetResult(FloorMap("Live Branch", "Locked", activeSessionId: null));
        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.False(viewModel.IsOffline);
        Assert.Empty(outbox.Pending);
        Assert.Empty(device.Dispatched);
        var note = Assert.Single(viewModel.OfflineActionAudit);
        Assert.Contains("PC-010", note);
    }

    private static FloorMapWorkspaceViewModel CreateViewModel(
        IOperatorFloorMapApiClient apiClient,
        IFloorMapCache cache,
        IOperatorDeviceApiClient device,
        IActionOutbox outbox)
    {
        return new FloorMapWorkspaceViewModel(
            apiClient,
            new UnconfiguredOperatorSessionApiClient(),
            new GuidIdempotencyKeyFactory(),
            "TJS",
            cache,
            new FixedTimeProvider(Now),
            device,
            outbox);
    }

    private static FloorMapDto FloorMap(string branchName, string state, Guid? activeSessionId)
    {
        return new FloorMapDto(
            BranchId,
            branchName,
            [
                new SeatStatusDto(
                    SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    SeatName: "PC-010",
                    ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
                    ZoneName: "Main Hall",
                    SortOrder: 1,
                    State: state,
                    DeviceId: DeviceId,
                    DeviceName: "PC-010",
                    IsDeviceOnline: true,
                    IsDeviceLocked: state != "Active",
                    LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-06-02T09:00:00Z"),
                    AgentVersion: "0.1.1",
                    ShellVersion: "0.1.2",
                    ActiveSessionId: activeSessionId,
                    RemainingSeconds: activeSessionId is null ? null : 1800)
            ]);
    }

    private sealed class StubFloorMapApiClient(FloorMapDto result) : IOperatorFloorMapApiClient
    {
        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
            => Task.FromResult(result);
    }

    private sealed class ThrowingFloorMapApiClient : IOperatorFloorMapApiClient
    {
        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
            => throw new HttpRequestException("network down");
    }

    private sealed class ToggleFloorMapApiClient : IOperatorFloorMapApiClient
    {
        private FloorMapDto? result;

        public void SetResult(FloorMapDto value) => result = value;

        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
        {
            if (result is null)
            {
                throw new HttpRequestException("network down");
            }

            return Task.FromResult(result);
        }
    }

    private sealed class RecordingDeviceApiClient : IOperatorDeviceApiClient
    {
        public List<(Guid DeviceId, string Type)> Dispatched { get; } = [];

        public Task<DeviceCommandDto> DispatchDeviceCommandAsync(
            Guid deviceId,
            string type,
            IReadOnlyDictionary<string, string> payload,
            CancellationToken cancellationToken)
        {
            Dispatched.Add((deviceId, type));
            return Task.FromResult(new DeviceCommandDto(
                CommandId: Guid.NewGuid(),
                Type: type,
                CreatedAtUtc: Now,
                Payload: payload.ToDictionary(pair => pair.Key, pair => pair.Value)));
        }

        public Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(Guid branchId, Guid organizationId, int expiresInSeconds, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DeviceCommandStatusDto> GetDeviceCommandStatusAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<DeviceDetailDto> GetDeviceDetailAsync(Guid deviceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RotateDeviceCredentialResponse> RotateDeviceCredentialAsync(Guid deviceId, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task<RevokeDeviceCredentialResponse> RevokeDeviceCredentialAsync(Guid deviceId, Guid credentialId, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }

    private sealed class InMemoryActionOutbox : IActionOutbox
    {
        private readonly List<OperatorActionOutboxEntry> pending = [];

        public IReadOnlyList<OperatorActionOutboxEntry> Pending => pending.ToArray();

        public void Enqueue(OperatorActionOutboxEntry entry)
        {
            pending.RemoveAll(existing => existing.DeviceId == entry.DeviceId);
            pending.Add(entry);
        }

        public void Acknowledge(Guid idempotencyKey)
        {
            pending.RemoveAll(existing => existing.IdempotencyKey == idempotencyKey);
        }
    }

    private sealed class SeededFloorMapCache : IFloorMapCache
    {
        private FloorMapCacheEntry? entry;

        public SeededFloorMapCache()
        {
        }

        public SeededFloorMapCache(FloorMapDto floorMap, DateTimeOffset cachedAtUtc)
        {
            entry = new FloorMapCacheEntry(floorMap, cachedAtUtc);
        }

        public void Save(FloorMapDto floorMap, DateTimeOffset cachedAtUtc)
        {
            entry = new FloorMapCacheEntry(floorMap, cachedAtUtc);
        }

        public FloorMapCacheEntry? Load(Guid branchId) => entry;
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
