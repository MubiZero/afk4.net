using System.Net.Http;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.OrganizationAdmin.App.Mvvm;
using AFK4.OrganizationAdmin.App.Sessions;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class FloorMapWorkspaceOfflineMirrorTests
{
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid DeviceId = Guid.Parse("33333333-3333-4333-8333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-06-02T10:00:00Z");

    [Fact]
    public async Task LoadAsync_OnSuccess_PersistsFloorMapToCacheAndStaysOnline()
    {
        var cache = new RecordingFloorMapCache();
        var apiClient = new StubFloorMapApiClient(FloorMap("Demo Branch", "PC-010", "Active"));
        var viewModel = CreateViewModel(apiClient, cache);

        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.False(viewModel.IsOffline);
        Assert.Null(viewModel.OfflineBanner);
        Assert.Equal(Now, viewModel.CachedAtUtc);
        var saved = Assert.Single(cache.Saved);
        Assert.Equal("Demo Branch", saved.FloorMap.BranchName);
        Assert.Equal(Now, saved.CachedAtUtc);
    }

    [Fact]
    public async Task LoadAsync_WhenRefreshFails_HydratesFromCacheAndEntersDegradedMode()
    {
        var cache = new RecordingFloorMapCache();
        cache.Seed(FloorMap("Cached Branch", "PC-042", "Active"), Now.AddMinutes(-5));
        var apiClient = new StubFloorMapApiClient(new HttpRequestException("network down"));
        var viewModel = CreateViewModel(apiClient, cache);

        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.True(viewModel.IsOffline);
        Assert.Equal(Now.AddMinutes(-5), viewModel.CachedAtUtc);
        Assert.Equal("Cached Branch", viewModel.BranchName);
        Assert.Equal("PC-042", Assert.Single(viewModel.Seats).Name);
        Assert.NotNull(viewModel.OfflineBanner);
        Assert.StartsWith("Офлайн", viewModel.OfflineBanner);
        Assert.EndsWith("только просмотр", viewModel.OfflineBanner);
        Assert.Null(viewModel.ErrorMessage);
        Assert.True(viewModel.SeatContext.IsOffline);
    }

    [Fact]
    public async Task LoadAsync_WhenRefreshFailsWithNoCache_FallsBackToErrorMessage()
    {
        var cache = new RecordingFloorMapCache();
        var apiClient = new StubFloorMapApiClient(new HttpRequestException("network down"));
        var viewModel = CreateViewModel(apiClient, cache);

        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.False(viewModel.IsOffline);
        Assert.Null(viewModel.OfflineBanner);
        Assert.Equal("network down", viewModel.ErrorMessage);
        Assert.Empty(viewModel.Seats);
        Assert.False(viewModel.SeatContext.IsOffline);
    }

    [Fact]
    public async Task LoadAsync_DisablesBillingActionsWhileOffline()
    {
        var cache = new RecordingFloorMapCache();
        cache.Seed(FloorMap("Cached Branch", "PC-042", "Active"), Now.AddMinutes(-5));
        var apiClient = new StubFloorMapApiClient(new HttpRequestException("network down"));
        var viewModel = CreateViewModel(apiClient, cache);

        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        viewModel.SelectedSeat = viewModel.Seats[0];

        Assert.True(viewModel.SeatContext.HasActiveSession);
        Assert.False(viewModel.SeatContext.EndSessionCommand.CanExecute(null));
        Assert.False(viewModel.SeatContext.ExtendSessionCommand.CanExecute(null));
        Assert.False(viewModel.SeatContext.BeginCheckoutCommand.CanExecute(null));
    }

    [Fact]
    public async Task LoadAsync_RecoversFromDegradedModeOnSuccessfulReload()
    {
        var cache = new RecordingFloorMapCache();
        cache.Seed(FloorMap("Cached Branch", "PC-042", "Active"), Now.AddMinutes(-5));
        var apiClient = new StubFloorMapApiClient(new HttpRequestException("network down"));
        var viewModel = CreateViewModel(apiClient, cache);
        await viewModel.LoadAsync(BranchId, CancellationToken.None);
        Assert.True(viewModel.IsOffline);

        apiClient.SetResult(FloorMap("Live Branch", "PC-099", "Free"));
        await viewModel.LoadAsync(BranchId, CancellationToken.None);

        Assert.False(viewModel.IsOffline);
        Assert.Null(viewModel.OfflineBanner);
        Assert.Equal("Live Branch", viewModel.BranchName);
        Assert.Equal("PC-099", Assert.Single(viewModel.Seats).Name);
        Assert.False(viewModel.SeatContext.IsOffline);
    }

    private static FloorMapWorkspaceViewModel CreateViewModel(IOperatorFloorMapApiClient apiClient, IFloorMapCache cache)
    {
        return new FloorMapWorkspaceViewModel(
            apiClient,
            new UnconfiguredOperatorSessionApiClient(),
            new GuidIdempotencyKeyFactory(),
            "TJS",
            cache,
            new FixedTimeProvider(Now));
    }

    private static FloorMapDto FloorMap(string branchName, string seatName, string state)
    {
        return new FloorMapDto(
            BranchId,
            branchName,
            [
                new SeatStatusDto(
                    SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    SeatName: seatName,
                    ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
                    ZoneName: "Main Hall",
                    SortOrder: 1,
                    State: state,
                    DeviceId: DeviceId,
                    DeviceName: seatName,
                    IsDeviceOnline: true,
                    IsDeviceLocked: state != "Active",
                    LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-06-02T09:00:00Z"),
                    AgentVersion: "0.1.1",
                    ShellVersion: "0.1.2",
                    ActiveSessionId: state == "Active" ? SessionId : null,
                    RemainingSeconds: state == "Active" ? 1800 : null)
            ]);
    }

    private sealed class StubFloorMapApiClient : IOperatorFloorMapApiClient
    {
        private FloorMapDto? result;
        private Exception? error;

        public StubFloorMapApiClient(FloorMapDto result) => this.result = result;

        public StubFloorMapApiClient(Exception error) => this.error = error;

        public void SetResult(FloorMapDto value)
        {
            result = value;
            error = null;
        }

        public Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
        {
            if (error is not null)
            {
                throw error;
            }

            return Task.FromResult(result!);
        }
    }

    private sealed class RecordingFloorMapCache : IFloorMapCache
    {
        private readonly Dictionary<Guid, FloorMapCacheEntry> entries = new();

        public List<FloorMapCacheEntry> Saved { get; } = [];

        public void Seed(FloorMapDto floorMap, DateTimeOffset cachedAtUtc)
        {
            entries[floorMap.BranchId] = new FloorMapCacheEntry(floorMap, cachedAtUtc);
        }

        public void Save(FloorMapDto floorMap, DateTimeOffset cachedAtUtc)
        {
            var entry = new FloorMapCacheEntry(floorMap, cachedAtUtc);
            entries[floorMap.BranchId] = entry;
            Saved.Add(entry);
        }

        public FloorMapCacheEntry? Load(Guid branchId)
        {
            return entries.TryGetValue(branchId, out var entry) ? entry : null;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
