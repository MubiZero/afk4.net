using System.Net;
using System.Net.Sockets;
using AFK4.Agent.Service.Network;
using AFK4.Shared.Contracts.Games;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Встроенный кеш игр на ПК администратора (docs/operations/game-cache-lancache.md): кусок игры качается из
/// интернета один раз и дальше отдаётся с диска; чужим сайтам он не прокси.
/// </summary>
public sealed class GameCacheServerTests : IAsyncDisposable
{
    private const string SteamUserAgent = "Valve/Steam HTTP Client 1.0";
    private readonly string root = Directory.CreateTempSubdirectory("afk4-game-cache-").FullName;
    private readonly int port = FreePort();
    private readonly CancellationTokenSource stop = new();
    private int upstreamCalls;
    private GameCacheServer? server;

    public async ValueTask DisposeAsync()
    {
        await stop.CancelAsync();
        if (server is not null) await server.StopAsync(CancellationToken.None);
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task AChunkComesFromTheInternetOnce_ThenFromDisk()
    {
        await StartAsync();
        var chunk = new byte[300_000];
        Random.Shared.NextBytes(chunk);
        using var client = new HttpClient();

        var first = await GetAsync(client, "cache1-fra1.steamcontent.com", "/depot/730/chunk/abc?sig=1", chunk);
        var second = await GetAsync(client, "cache2-ams1.steamcontent.com", "/depot/730/chunk/abc?sig=2", chunk);

        Assert.Equal("MISS", first.Status);
        Assert.Equal(chunk, first.Body);
        // Другой сервер CDN, другая подпись — тот же кусок Steam: отдан с диска, в интернет не ходили.
        Assert.Equal("HIT", second.Status);
        Assert.Equal(chunk, second.Body);
        Assert.Equal(1, upstreamCalls);
    }

    [Fact]
    public async Task OnlySteamAndEpicAreProxied_AndTheHeartbeatAnswersLikeLanCache()
    {
        await StartAsync();
        using var client = new HttpClient();

        using var heartbeat = await client.GetAsync($"http://127.0.0.1:{port}{GameCacheNames.HeartbeatPath}");
        Assert.Equal(HttpStatusCode.NoContent, heartbeat.StatusCode);
        Assert.True(heartbeat.Headers.Contains(GameCacheNames.HeartbeatHeader));

        using var foreign = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}/");
        foreign.Headers.Host = "example.com";
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreign)).StatusCode);
        Assert.Equal(0, upstreamCalls);
    }

    [Theory]
    // Диск 1 ТБ, свободно 600 ГБ: запас 150 ГБ (15 %), половина доступного — 300 ГБ.
    [InlineData(1000L, 600L, 0L, 300L)]
    // Почти полный диск: запас важнее — кеш не растёт.
    [InlineData(256L, 20L, 0L, 0L)]
    // Кеш уже занимает своё: своё входит в доступное.
    [InlineData(1000L, 400L, 200L, 300L)]
    public void TheCacheTakesAtMostHalf_AndAlwaysLeavesAReserve(long totalGiB, long freeGiB, long cacheGiB, long expectedGiB)
    {
        const long GiB = 1024L * 1024 * 1024;
        Assert.Equal(expectedGiB * GiB, GameCacheStore.QuotaBytes(totalGiB * GiB, freeGiB * GiB, cacheGiB * GiB));
    }

    private async Task StartAsync()
    {
        server = new GameCacheServer(
            new StubFactory(_ => { Interlocked.Increment(ref upstreamCalls); return null!; }) { Body = () => currentBody },
            NullLogger<GameCacheServer>.Instance,
            root,
            $"http://+:{port}/",
            fixedQuotaBytes: GameCacheStore.MinUsefulQuotaBytes);
        await server.StartAsync(stop.Token);
        server.Configure(host: true);
        for (var attempt = 0; attempt < 50 && !server.Serving; attempt++) await Task.Delay(20);
        Assert.True(server.Serving);
    }

    private async Task<(string? Status, byte[] Body)> GetAsync(HttpClient client, string host, string pathAndQuery, byte[] upstreamBody)
    {
        currentBody = upstreamBody;
        using var request = new HttpRequestMessage(HttpMethod.Get, $"http://127.0.0.1:{port}{pathAndQuery}");
        request.Headers.Host = host;
        request.Headers.TryAddWithoutValidation("User-Agent", SteamUserAgent);
        using var response = await client.SendAsync(request);
        return (response.Headers.TryGetValues("X-Cache-Status", out var values) ? values.Single() : null, await response.Content.ReadAsByteArrayAsync());
    }

    private byte[] currentBody = [];

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    private sealed class StubFactory(Func<HttpRequestMessage, HttpResponseMessage> onCall) : IHttpClientFactory
    {
        public Func<byte[]>? Body { get; set; }

        public HttpClient CreateClient(string name) => new(new Handler(this, onCall));

        private sealed class Handler(StubFactory owner, Func<HttpRequestMessage, HttpResponseMessage> onCall) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                onCall(request);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(owner.Body!()) });
            }
        }
    }
}
