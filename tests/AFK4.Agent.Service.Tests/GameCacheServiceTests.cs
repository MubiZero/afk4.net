using System.Net;
using AFK4.Agent.Service.Network;
using AFK4.Shared.Contracts.Games;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests;

/// <summary>
/// Кеш игр клуба (docs/operations/game-cache-lancache.md): пока кеш отвечает, имена Steam и Epic в hosts
/// ведут к нему; перестал — строки убраны, иначе Epic упёрся бы в мёртвый адрес. Чужие строки hosts целы.
/// </summary>
public sealed class GameCacheServiceTests : IDisposable
{
    private const string ForeignHosts = "# Copyright (c) Microsoft\r\n127.0.0.1 localhost\r\n10.0.0.5 printer.local\r\n";
    private readonly string hostsPath = Path.Combine(Directory.CreateTempSubdirectory("afk4-hosts-").FullName, "hosts");

    public void Dispose() => Directory.Delete(Path.GetDirectoryName(hostsPath)!, recursive: true);

    [Fact]
    public void TheBlockIsAddedReplacedAndRemoved_LeavingForeignLinesAlone()
    {
        var added = GameCacheHosts.Rewrite(ForeignHosts, "192.168.1.50");
        Assert.StartsWith(ForeignHosts.TrimEnd(), added);
        Assert.Contains($"192.168.1.50 {GameCacheNames.SteamDiscoveryHost}\r\n", added);

        var moved = GameCacheHosts.Rewrite(added, "192.168.1.60");
        Assert.DoesNotContain("192.168.1.50", moved);
        Assert.Single(moved.Split(GameCacheHosts.BeginMarker)[1..]);

        Assert.Equal(ForeignHosts, GameCacheHosts.Rewrite(moved, null));
    }

    [Fact]
    public async Task WhileTheCacheAnswers_SteamGoesThroughIt_WhenItStops_TheLinesAreRemoved()
    {
        await File.WriteAllTextAsync(hostsPath, ForeignHosts);
        var cacheUp = true;
        var service = new GameCacheService(
            new StubHttpClientFactory(_ => cacheUp ? new HttpResponseMessage(HttpStatusCode.NoContent) : throw new HttpRequestException("down")),
            NullLogger<GameCacheService>.Instance,
            hostsPath);

        service.Configure("192.168.1.50");
        await service.CheckAsync(CancellationToken.None);
        Assert.True(service.Reachable);
        Assert.Contains($"192.168.1.50 {GameCacheNames.SteamDiscoveryHost}", await File.ReadAllTextAsync(hostsPath));

        cacheUp = false;
        await service.CheckAsync(CancellationToken.None);
        Assert.False(service.Reachable);
        Assert.Equal(ForeignHosts, await File.ReadAllTextAsync(hostsPath));
    }

    // Клуб убрал кеш в Панели (пустой адрес) — строки уходят; сервер прежней версии (null) ничего не меняет.
    [Fact]
    public async Task AnEmptyAddressClearsTheCache_AMissingOneChangesNothing()
    {
        await File.WriteAllTextAsync(hostsPath, ForeignHosts);
        var service = new GameCacheService(
            new StubHttpClientFactory(_ => new HttpResponseMessage(HttpStatusCode.NoContent)), NullLogger<GameCacheService>.Instance, hostsPath);
        service.Configure("192.168.1.50");
        await service.CheckAsync(CancellationToken.None);

        service.Configure(null);
        await service.CheckAsync(CancellationToken.None);
        Assert.Contains(GameCacheHosts.BeginMarker, await File.ReadAllTextAsync(hostsPath));

        service.Configure(string.Empty);
        await service.CheckAsync(CancellationToken.None);
        Assert.Null(service.Reachable);
        Assert.Equal(ForeignHosts, await File.ReadAllTextAsync(hostsPath));
    }

    // Адрес не из сети клуба в hosts не попадает: иначе загрузки всех ПК ушли бы на чужой сервер.
    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("192.168.1.50 evil.example")]
    [InlineData("::1")]
    public void OnlyAPrivateIPv4IsACacheAddress(string address) => Assert.False(GameCacheNames.IsValidAddress(address));

    private sealed class StubHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> respond) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(new StubHandler(respond));
    }

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }
}
