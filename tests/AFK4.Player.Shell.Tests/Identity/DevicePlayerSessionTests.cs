using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using AFK4.Player.Shell.Identity;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Player.Shell.Tests.Identity;

/// <summary>Вход игрока в памяти хоста: обновление заранее, выход — с гашением на сервере, мигание сети вход не рвёт.</summary>
public sealed class DevicePlayerSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-09-25T10:00:00Z");

    [Fact]
    public void AnAcceptedSignIn_IsWhoIsAtThePc()
    {
        var session = new Fixture().Session;

        var auth = session.Accept(Session(expiresIn: TimeSpan.FromMinutes(15)));

        Assert.True(auth.SignedIn);
        Assert.Equal("Фарход", auth.DisplayName);
        Assert.Equal("access-1", session.AccessToken);
    }

    [Fact]
    public async Task AFreshToken_IsNotRefreshed()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(15)));

        Assert.False(await fixture.Session.EnsureFreshAsync(CancellationToken.None));
        Assert.Empty(fixture.Handler.Paths);
    }

    [Fact]
    public async Task AnExpiringToken_IsRefreshedAhead()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(1)));
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(Session(expiresIn: TimeSpan.FromMinutes(15), access: "access-2"))
        };

        Assert.False(await fixture.Session.EnsureFreshAsync(CancellationToken.None));

        Assert.Equal(["/api/public/player/refresh"], fixture.Handler.Paths);
        Assert.Equal("access-2", fixture.Session.AccessToken);
    }

    [Fact]
    public async Task ARefusedRefresh_EndsTheSignIn()
    {
        // Сессия закончилась, сервер погасил токены — страница должна узнать, что за ПК никого нет.
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(1)));
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.Unauthorized);

        Assert.True(await fixture.Session.EnsureFreshAsync(CancellationToken.None));
        Assert.False(fixture.Session.Current.SignedIn);
    }

    [Fact]
    public async Task ANetworkBlip_KeepsThePlayerSignedIn()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(1)));
        fixture.Handler.Respond = _ => throw new HttpRequestException("offline");

        Assert.False(await fixture.Session.EnsureFreshAsync(CancellationToken.None));
        Assert.True(fixture.Session.Current.SignedIn);
    }

    [Fact]
    public async Task SigningOut_RevokesOnTheServer_WithBothTokens()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(15)));
        fixture.Handler.Respond = _ => new HttpResponseMessage(HttpStatusCode.NoContent);

        await fixture.Session.SignOutAsync(CancellationToken.None);

        Assert.False(fixture.Session.Current.SignedIn);
        Assert.Equal(["/api/public/player/sign-out"], fixture.Handler.Paths);
        Assert.Equal("Bearer access-1", fixture.Handler.Authorization);
        Assert.Contains("refresh-1", fixture.Handler.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SigningOut_WithoutNetwork_StillForgetsThePlayer()
    {
        var fixture = new Fixture();
        fixture.Session.Accept(Session(expiresIn: TimeSpan.FromMinutes(15)));
        fixture.Handler.Respond = _ => throw new HttpRequestException("offline");

        await fixture.Session.SignOutAsync(CancellationToken.None);

        Assert.False(fixture.Session.Current.SignedIn);
    }

    private static PlatformPersonSessionResponse Session(TimeSpan expiresIn, string access = "access-1") => new(
        Guid.NewGuid(), Guid.NewGuid(), "Фарход", true, access, Now + expiresIn,
        "refresh-1", Now.AddHours(12), Guid.NewGuid(), "ru", true);

    private sealed class Fixture
    {
        public Fixture()
        {
            Session = new DevicePlayerSession(new HttpClient(Handler), () => "https://api.example.test/", new FixedClock(Now));
        }

        public StubHandler Handler { get; } = new();

        public DevicePlayerSession Session { get; }
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        public Func<HttpRequestMessage, HttpResponseMessage> Respond { get; set; } =
            _ => new HttpResponseMessage(HttpStatusCode.InternalServerError);

        public List<string> Paths { get; } = [];

        public string? Authorization { get; private set; }

        public string Body { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.AbsolutePath);
            Authorization = request.Headers.Authorization?.ToString();
            Body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            return Respond(request);
        }
    }
}
