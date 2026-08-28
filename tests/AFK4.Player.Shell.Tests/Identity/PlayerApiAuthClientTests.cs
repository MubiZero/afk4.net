using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Tests.Identity;

public sealed class PlayerApiAuthClientTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add(request);
            return Task.FromResult(responder(request));
        }
    }

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static HttpResponseMessage Ok(PlayerSignInResponse body) =>
        new(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: Json) };

    private static PlayerSignInResponse Response(string access, string refresh, DateTimeOffset accessExp) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Alex", true, access, accessExp, refresh, DateTimeOffset.UtcNow.AddDays(30));

    [Fact]
    public async Task SignIn_Success_StoresTokensAndReturnsSnapshotWithoutSecret()
    {
        var handler = new StubHandler(_ => Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1))));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        var snapshot = await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", null, CancellationToken.None);

        Assert.True(snapshot.Authenticated);
        Assert.Equal("Alex", snapshot.DisplayName);
        Assert.True(snapshot.PhoneVerified);
        Assert.Equal("acc-1", client.CurrentAccessToken);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/public/player/sign-in");
    }

    // Зал уезжает на сервер вместе с номером и PIN: без него первый вход человека в сеть из
    // нескольких залов сервер отклонит, а экран ПК покажет тот же отказ, что и при неверном PIN.
    [Fact]
    public async Task SignIn_SendsTheBranchTheComputerStandsIn()
    {
        var handler = new StubHandler(_ => Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1))));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });
        var branch = Guid.NewGuid();

        await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", branch, CancellationToken.None);

        var sent = await handler.Requests.Single().Content!.ReadFromJsonAsync<PlayerSignInRequest>(Json);
        Assert.Equal(branch, sent!.BranchId);
    }

    [Fact]
    public async Task SignIn_Unauthorized_ReturnsUnauthenticatedAndHoldsNoToken()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        var snapshot = await client.SignInAsync(Guid.NewGuid(), "+992900000000", "bad", null, CancellationToken.None);

        Assert.False(snapshot.Authenticated);
        Assert.Null(client.CurrentAccessToken);
    }

    [Fact]
    public async Task EnsureFreshToken_RefreshesWhenAccessExpired()
    {
        var calls = 0;
        var handler = new StubHandler(req =>
        {
            calls++;
            return req.RequestUri!.AbsolutePath == "/api/public/player/refresh"
                ? Ok(Response("acc-2", "ref-2", DateTimeOffset.UtcNow.AddHours(1)))
                : Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddSeconds(-5)));
        });
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", null, CancellationToken.None);
        await client.EnsureFreshTokenAsync(CancellationToken.None);

        Assert.Equal("acc-2", client.CurrentAccessToken);
        Assert.Contains(handler.Requests, r => r.RequestUri!.AbsolutePath == "/api/public/player/refresh");
    }

    [Fact]
    public async Task SignOut_ClearsToken()
    {
        var handler = new StubHandler(_ => Ok(Response("acc-1", "ref-1", DateTimeOffset.UtcNow.AddHours(1))));
        var client = new PlayerApiAuthClient(new HttpClient(handler) { BaseAddress = new Uri("https://api.test") });

        await client.SignInAsync(Guid.NewGuid(), "+992900000000", "pw", null, CancellationToken.None);
        client.SignOut();

        Assert.Null(client.CurrentAccessToken);
        Assert.False(client.Current.Authenticated);
    }
}
