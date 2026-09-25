using System.Net;
using System.Net.Http;
using AFK4.Agent.Service.Showcase;
using AFK4.Agent.Service.Tests.Games;
using AFK4.Shared.Contracts.Showcase;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests.Showcase;

public sealed class ShowcaseServiceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-25T18:00:00Z");

    private static ShowcaseCardDto News(string id = "news:1", string? image = "https://media.afk4.net/n.webp") =>
        new(id, ShowcaseCardKindNames.News, "Ночь CS2", "В пятницу", ImageUrl: image);

    [Fact]
    public async Task AFreshShowcase_IsStored_AndServedWithCachedImages()
    {
        var fixture = new Fixture();
        fixture.Client.Next = new ShowcaseFetch(new DeviceShowcaseDto([News(), News("news:2", image: null)]), "\"v1\"");

        await fixture.Service.SyncIfDueAsync(CancellationToken.None);

        Assert.Equal("\"v1\"", fixture.Store.Saved!.ETag);
        var cards = fixture.Service.Cards();
        Assert.Equal(["news:1", "news:2"], cards.Select(card => card.CardId));
        // Экран получает адрес из кэша ПК, а не из медиа-хранилища.
        Assert.Equal("https://showcase.afk4.local/cards/n.webp", cards[0].ImageUrl);
        Assert.Null(cards[1].ImageUrl);
        Assert.Equal(1, fixture.Signal.Count);
    }

    [Fact]
    public async Task TheServer_IsAskedEveryTenMinutes_WithThePreviousETag()
    {
        var fixture = new Fixture(new StoredShowcase("\"v1\"", [News()]));
        fixture.Client.Next = new ShowcaseFetch(null, "\"v1\"");

        await fixture.Service.SyncIfDueAsync(CancellationToken.None);
        await fixture.Service.SyncIfDueAsync(CancellationToken.None);
        Assert.Equal(1, fixture.Client.Calls);
        Assert.Equal("\"v1\"", fixture.Client.LastETag);
        // 304 не трогает сохранённое, но картинки докачиваются.
        Assert.Null(fixture.Store.Saved);
        Assert.Equal(1, fixture.Images.Refreshes);
        Assert.Equal("news:1", Assert.Single(fixture.Service.Cards()).CardId);

        fixture.Time.Advance(ShowcaseService.RefreshEvery);
        await fixture.Service.SyncIfDueAsync(CancellationToken.None);
        Assert.Equal(2, fixture.Client.Calls);
    }

    [Fact]
    public async Task WithoutTheNetwork_TheCachedShowcaseStays_AndARetryComesSooner()
    {
        var fixture = new Fixture(new StoredShowcase("\"v1\"", [News()]));
        fixture.Client.Failure = new HttpRequestException("offline");

        await fixture.Service.SyncIfDueAsync(CancellationToken.None);

        Assert.Equal("news:1", Assert.Single(fixture.Service.Cards()).CardId);
        fixture.Time.Advance(ShowcaseService.RetryAfterFailure);
        await fixture.Service.SyncIfDueAsync(CancellationToken.None);
        Assert.Equal(2, fixture.Client.Calls);
    }

    private sealed class Fixture
    {
        public Fixture(StoredShowcase? stored = null)
        {
            Store = new MemoryStore(stored);
            Service = new ShowcaseService(Store, Client, Images, Signal, Time, NullLogger<ShowcaseService>.Instance);
        }

        public MemoryStore Store { get; }

        public FakeClient Client { get; } = new();

        public FakeImages Images { get; } = new();

        public GameLibraryServiceTests.CountingSignal Signal { get; } = new();

        public MovableTime Time { get; } = new(Start);

        public ShowcaseService Service { get; }
    }

    private sealed class MemoryStore(StoredShowcase? initial) : IShowcaseStore
    {
        public StoredShowcase? Saved { get; private set; }

        public StoredShowcase? Load() => initial;

        public void Save(StoredShowcase showcase) => Saved = showcase;
    }

    private sealed class FakeClient : IShowcaseClient
    {
        public ShowcaseFetch Next { get; set; } = new(new DeviceShowcaseDto([]), null);

        public Exception? Failure { get; set; }

        public int Calls { get; private set; }

        public string? LastETag { get; private set; }

        public Task<ShowcaseFetch> GetAsync(string? etag, CancellationToken cancellationToken)
        {
            Calls++;
            LastETag = etag;
            return Failure is null ? Task.FromResult(Next) : Task.FromException<ShowcaseFetch>(Failure);
        }
    }

    private sealed class FakeImages : IShowcaseImageCache
    {
        public int Refreshes { get; private set; }

        public string? PageUri(string? remoteUrl) =>
            remoteUrl is null ? null : $"https://showcase.afk4.local/cards/{remoteUrl[(remoteUrl.LastIndexOf('/') + 1)..]}";

        public Task RefreshAsync(IReadOnlyList<ShowcaseCardDto> cards, CancellationToken cancellationToken)
        {
            Refreshes++;
            return Task.CompletedTask;
        }
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}

public sealed class FileShowcaseImageCacheTests : IDisposable
{
    private readonly TemporaryDirectory directory = TemporaryDirectory.Create();

    public void Dispose() => directory.Dispose();

    private static ShowcaseCardDto Card(string id, string? image) => new(id, ShowcaseCardKindNames.News, id, ImageUrl: image);

    [Fact]
    public async Task AnImage_IsDownloadedOnce_AndGoneWithItsCard()
    {
        var handler = new Handler(_ => Image("image/webp", 10));
        var cache = new FileShowcaseImageCache(new Factory(handler), NullLogger<FileShowcaseImageCache>.Instance, directory.Path);
        var card = Card("news:1", "https://media.afk4.net/org/branch/a.webp");

        await cache.RefreshAsync([card], CancellationToken.None);
        await cache.RefreshAsync([card], CancellationToken.None);

        Assert.Equal(1, handler.Calls);
        var uri = cache.PageUri(card.ImageUrl);
        Assert.StartsWith("https://showcase.afk4.local/cards/", uri, StringComparison.Ordinal);
        Assert.EndsWith(".webp", uri, StringComparison.Ordinal);

        await cache.RefreshAsync([], CancellationToken.None);
        Assert.Null(cache.PageUri(card.ImageUrl));
    }

    [Fact]
    public async Task APlainHttpAddress_OrAnOversizedFile_IsNotShown()
    {
        var handler = new Handler(_ => Image("image/png", (int)FileShowcaseImageCache.MaxBytes + 1));
        var cache = new FileShowcaseImageCache(new Factory(handler), NullLogger<FileShowcaseImageCache>.Instance, directory.Path);

        await cache.RefreshAsync([Card("big", "https://media.afk4.net/big.png"), Card("plain", "http://media.afk4.net/a.png")], CancellationToken.None);

        Assert.Null(cache.PageUri("https://media.afk4.net/big.png"));
        Assert.Null(cache.PageUri("http://media.afk4.net/a.png"));
        Assert.Equal(1, handler.Calls);
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
