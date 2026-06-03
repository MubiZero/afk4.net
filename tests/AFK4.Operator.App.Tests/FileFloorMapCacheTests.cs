using AFK4.Operator.App.FloorMap;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.Operator.App.Tests;

public sealed class FileFloorMapCacheTests
{
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid OtherBranchId = Guid.Parse("bd0f1234-5678-4abc-9def-0123456789ab");
    private static readonly DateTimeOffset CachedAt = DateTimeOffset.Parse("2026-06-02T08:15:00Z");

    [Fact]
    public void Load_WhenNoCacheFileExists_ReturnsNull()
    {
        using var directory = new TemporaryDirectory();
        var cache = new FileFloorMapCache(directory.Path);

        Assert.Null(cache.Load(BranchId));
    }

    [Fact]
    public void Save_ThenLoad_RoundTripsFloorMapAndCachedAt()
    {
        using var directory = new TemporaryDirectory();
        var cache = new FileFloorMapCache(directory.Path);
        var floorMap = CreateFloorMap(BranchId, "Demo Branch", "PC-010");

        cache.Save(floorMap, CachedAt);

        var entry = cache.Load(BranchId);
        Assert.NotNull(entry);
        Assert.Equal(CachedAt, entry!.CachedAtUtc);
        Assert.Equal("Demo Branch", entry.FloorMap.BranchName);
        Assert.Equal("PC-010", entry.FloorMap.Seats[0].SeatName);
    }

    [Fact]
    public void Save_SurvivesAcrossInstances()
    {
        using var directory = new TemporaryDirectory();
        new FileFloorMapCache(directory.Path).Save(CreateFloorMap(BranchId, "Demo Branch", "PC-010"), CachedAt);

        var reopened = new FileFloorMapCache(directory.Path);

        Assert.NotNull(reopened.Load(BranchId));
    }

    [Fact]
    public void Save_ReplacesPreviousEntryForSameBranch()
    {
        using var directory = new TemporaryDirectory();
        var cache = new FileFloorMapCache(directory.Path);
        cache.Save(CreateFloorMap(BranchId, "Old Name", "PC-001"), CachedAt);

        cache.Save(CreateFloorMap(BranchId, "New Name", "PC-002"), CachedAt.AddMinutes(5));

        var entry = cache.Load(BranchId);
        Assert.Equal("New Name", entry!.FloorMap.BranchName);
        Assert.Equal("PC-002", entry.FloorMap.Seats[0].SeatName);
        Assert.Equal(CachedAt.AddMinutes(5), entry.CachedAtUtc);
    }

    [Fact]
    public void Load_IsolatesEntriesPerBranch()
    {
        using var directory = new TemporaryDirectory();
        var cache = new FileFloorMapCache(directory.Path);
        cache.Save(CreateFloorMap(BranchId, "Branch A", "PC-001"), CachedAt);

        Assert.Null(cache.Load(OtherBranchId));
        Assert.NotNull(cache.Load(BranchId));
    }

    [Fact]
    public void Load_WhenCacheFileIsCorrupt_ReturnsNullAndSelfHeals()
    {
        using var directory = new TemporaryDirectory();
        var cache = new FileFloorMapCache(directory.Path);
        cache.Save(CreateFloorMap(BranchId, "Demo Branch", "PC-010"), CachedAt);
        var cacheFile = Directory.GetFiles(directory.Path).Single();
        File.WriteAllText(cacheFile, "{ this is not valid json");

        Assert.Null(cache.Load(BranchId));
        Assert.False(File.Exists(cacheFile));
    }

    private static FloorMapDto CreateFloorMap(Guid branchId, string branchName, string seatName)
    {
        return new FloorMapDto(
            branchId,
            branchName,
            [
                new SeatStatusDto(
                    SeatId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
                    SeatName: seatName,
                    ZoneId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
                    ZoneName: "Main Hall",
                    SortOrder: 1,
                    State: "Active",
                    DeviceId: Guid.Parse("33333333-3333-4333-8333-333333333333"),
                    DeviceName: seatName,
                    IsDeviceOnline: true,
                    IsDeviceLocked: false,
                    LastHeartbeatAtUtc: DateTimeOffset.Parse("2026-06-02T08:00:00Z"),
                    AgentVersion: "0.1.1",
                    ShellVersion: "0.1.2",
                    ActiveSessionId: Guid.Parse("44444444-4444-4444-8444-444444444444"),
                    RemainingSeconds: 1800)
            ]);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"afk4-floormap-cache-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
        }
    }
}
