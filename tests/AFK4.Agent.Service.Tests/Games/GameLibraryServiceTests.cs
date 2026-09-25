using System.Net;
using System.Net.Http;
using AFK4.Agent.Service.Games;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Games;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests.Games;

public sealed class GameLibraryServiceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T18:00:00Z");

    private static DeviceGameDto Dota(string? cover = null) =>
        new("g1", "Dota 2", "MOBA", 12, cover, GameLaunchKindNames.Steam, "570", null, null, false);

    [Fact]
    public void WithoutAClubLibrary_TheBootstrapListStays()
    {
        var fixture = new Fixture(stored: null);

        var entry = Assert.Single(fixture.Service.Entries());

        Assert.Equal("notepad", entry.AppId);
        Assert.Equal(@"C:\Windows\notepad.exe", entry.ExecutablePath);
    }

    [Fact]
    public void TheClubLibrary_ReplacesIt_WithLaunchesForThisPc()
    {
        var fixture = new Fixture(stored: new DeviceGameLibraryDto(3, [Dota()]));

        var entry = Assert.Single(fixture.Service.Entries());

        Assert.Equal("Dota 2", entry.DisplayName);
        Assert.Equal("MOBA", entry.Category);
        Assert.Equal(12, entry.MinAge);
        Assert.Equal(@"D:\Steam\steam.exe", entry.ExecutablePath);
        Assert.Equal("-applaunch 570", entry.Arguments);
        // Запуск ищет игру так же, как её показывают: без учёта регистра имени.
        Assert.Equal("g1", fixture.Service.Find("G1")!.AppId);
    }

    [Fact]
    public async Task ANewVersion_IsFetched_Stored_AndShownToTheShell()
    {
        var fixture = new Fixture(stored: null);
        fixture.Client.Library = new DeviceGameLibraryDto(2, [Dota("https://media.afk4.net/dota.webp")]);

        await fixture.Service.SyncAsync(2, CancellationToken.None);
        await fixture.Service.SyncAsync(2, CancellationToken.None);

        Assert.Equal(1, fixture.Client.Calls);
        Assert.Equal(2, fixture.Store.Saved!.Version);
        Assert.Equal(1, fixture.Covers.Refreshes);
        Assert.Equal(1, fixture.Signal.Count);
        Assert.Equal("Dota 2", Assert.Single(fixture.Service.Entries()).DisplayName);
    }

    // Сервер не ответил — ПК остаётся с прежней библиотекой и не долбит сервер каждые десять секунд.
    [Fact]
    public async Task AFailedFetch_KeepsTheOldLibrary_AndWaitsBeforeTheNextTry()
    {
        var fixture = new Fixture(stored: new DeviceGameLibraryDto(1, [Dota()]));
        fixture.Client.Failure = new HttpRequestException("no route");

        await fixture.Service.SyncAsync(2, CancellationToken.None);
        await fixture.Service.SyncAsync(2, CancellationToken.None);

        Assert.Equal(1, fixture.Client.Calls);
        Assert.Equal("Dota 2", Assert.Single(fixture.Service.Entries()).DisplayName);
    }

    private sealed class Fixture
    {
        public Fixture(DeviceGameLibraryDto? stored)
        {
            Store = new MemoryStore(stored);
            var options = Options.Create(new AgentOptions
            {
                LauncherApps = [new AgentLauncherAppOptions { AppId = "notepad", DisplayName = "Блокнот", ExecutablePath = @"C:\Windows\notepad.exe" }]
            });
            Service = new GameLibraryService(options, Store, Client, Covers, new Locator(), Signal, new FixedTime(), NullLogger<GameLibraryService>.Instance);
        }

        public MemoryStore Store { get; }
        public FakeClient Client { get; } = new();
        public FakeCovers Covers { get; } = new();
        public CountingSignal Signal { get; } = new();
        public GameLibraryService Service { get; }
    }

    internal sealed class MemoryStore(DeviceGameLibraryDto? initial) : IGameLibraryStore
    {
        public DeviceGameLibraryDto? Saved { get; private set; }

        public DeviceGameLibraryDto? Load() => Saved ?? initial;

        public void Save(DeviceGameLibraryDto library) => Saved = library;
    }

    internal sealed class FakeClient : IGameLibraryClient
    {
        public DeviceGameLibraryDto Library { get; set; } = new(0, []);
        public Exception? Failure { get; set; }
        public int Calls { get; private set; }

        public Task<DeviceGameLibraryDto> GetAsync(CancellationToken cancellationToken)
        {
            Calls++;
            return Failure is null ? Task.FromResult(Library) : Task.FromException<DeviceGameLibraryDto>(Failure);
        }
    }

    internal sealed class FakeCovers : ICoverCache
    {
        public int Refreshes { get; private set; }

        public string? CoverUri(string appId) => null;

        public Task RefreshAsync(IReadOnlyList<DeviceGameDto> games, CancellationToken cancellationToken)
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }

    internal sealed class CountingSignal : IShellStateSignal
    {
        public int Count { get; private set; }

        public void Notify() => Count++;

        public Task WaitAsync(TimeSpan timeout, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class Locator : IGameLauncherLocator
    {
        public string? SteamExecutable() => @"D:\Steam\steam.exe";
        public string? EpicLauncherExecutable() => null;
        public string? RiotClientExecutable() => null;
        public string? BattleNetExecutable() => null;
        public string UrlOpener() => "rundll32.exe";
    }

    private sealed class FixedTime : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public sealed class FileCoverCacheTests : IDisposable
{
    private readonly TemporaryDirectory directory = TemporaryDirectory.Create();

    public void Dispose() => directory.Dispose();

    private static DeviceGameDto Game(string appId, string? cover) =>
        new(appId, appId, null, null, cover, GameLaunchKindNames.Steam, "1", null, null, false);

    [Fact]
    public async Task DownloadsAnImage_AndServesItFromTheShowcaseHost()
    {
        var handler = new Handler(_ => Image("image/webp", 10));
        var cache = new FileCoverCache(new Factory(handler), NullLogger<FileCoverCache>.Instance, directory.Path);

        await cache.RefreshAsync([Game("g1", "https://media.afk4.net/games/dota.webp")], CancellationToken.None);

        var uri = cache.CoverUri("g1");
        Assert.NotNull(uri);
        Assert.StartsWith("https://showcase.afk4.local/covers/g1.", uri, StringComparison.Ordinal);
        Assert.EndsWith(".webp", uri, StringComparison.Ordinal);

        // Та же обложка второй раз не качается.
        await cache.RefreshAsync([Game("g1", "https://media.afk4.net/games/dota.webp")], CancellationToken.None);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task NotAnImage_OrTooBig_OrNotHttps_IsNotKept()
    {
        var handler = new Handler(request => request.RequestUri!.AbsolutePath.Contains("big")
            ? Image("image/png", (int)FileCoverCache.MaxBytes + 1)
            : Image("text/html", 10));
        var cache = new FileCoverCache(new Factory(handler), NullLogger<FileCoverCache>.Instance, directory.Path);

        await cache.RefreshAsync(
            [Game("page", "https://media.afk4.net/page.webp"), Game("big", "https://media.afk4.net/big.png"), Game("plain", "http://media.afk4.net/a.png")],
            CancellationToken.None);

        Assert.Null(cache.CoverUri("page"));
        Assert.Null(cache.CoverUri("big"));
        Assert.Null(cache.CoverUri("plain"));
        Assert.Equal(2, handler.Calls);
    }

    [Fact]
    public async Task ARemovedGame_LeavesNoCoverBehind()
    {
        var cache = new FileCoverCache(new Factory(new Handler(_ => Image("image/jpeg", 10))), NullLogger<FileCoverCache>.Instance, directory.Path);
        await cache.RefreshAsync([Game("g1", "https://media.afk4.net/a.jpg")], CancellationToken.None);

        await cache.RefreshAsync([], CancellationToken.None);

        Assert.Null(cache.CoverUri("g1"));
    }

    private static HttpResponseMessage Image(string mediaType, int size)
    {
        var content = new ByteArrayContent(new byte[size]);
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(mediaType);
        return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
    }

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class Factory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }
}
