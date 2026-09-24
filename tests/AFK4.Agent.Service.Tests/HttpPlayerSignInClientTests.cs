using System.Net;
using System.Net.Http.Json;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

/// <summary>Путь входа к серверу: ключ ПК в заголовке, отказ по правилу — кодом, сбой пути — исключением.</summary>
public sealed class HttpPlayerSignInClientTests
{
    private static readonly AgentOptions Options = new()
    {
        OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        DeviceId = Guid.Parse("d76eff15-9cf9-4c30-a6d4-c05fd215793f"),
        PlatformBaseUrl = new Uri("https://api.example.test/")
    };

    [Fact]
    public async Task APinSignIn_GoesWithTheDeviceKey_AndReturnsTheSession()
    {
        var handler = new StubHandler(HttpStatusCode.OK, JsonContent.Create(Session()));

        var outcome = await Client(handler).SignInWithPinAsync("+992900000007", "123456", CancellationToken.None);

        Assert.Equal("access-token", outcome.Session!.AccessToken);
        Assert.Equal($"/api/devices/{Options.DeviceId:D}/player-sign-in", handler.Request!.RequestUri!.AbsolutePath);
        Assert.Equal("rotated-secret", handler.Request.Headers.GetValues(DeviceCredentialHeaders.CredentialSecret).Single());
        var body = System.Text.Json.JsonSerializer.Deserialize<DevicePlayerSignInRequest>(
            handler.Body!, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.Equal(("+992900000007", "123456", Options.DeviceId), (body!.PhoneNumber, body.Pin, body.DeviceId));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, DevicePlayerSignInErrorCodeNames.SignInRefused)]
    [InlineData(HttpStatusCode.TooManyRequests, DevicePlayerSignInErrorCodeNames.TooManyAttempts)]
    [InlineData(HttpStatusCode.Conflict, DevicePlayerSignInErrorCodeNames.SessionNotYours)]
    public async Task ARefusalByRule_ComesBackAsItsCode(HttpStatusCode status, string code)
    {
        var handler = new StubHandler(status, JsonContent.Create(new DevicePlayerSignInErrorDto(code)));

        var outcome = await Client(handler).SignInWithPinAsync("+992900000007", "000000", CancellationToken.None);

        Assert.Null(outcome.Session);
        Assert.Equal(code, outcome.ErrorCode);
    }

    [Fact]
    public async Task ARefusedDeviceKey_IsABrokenRoad_NotAWrongPin()
    {
        // Пустой 401 — ключ ПК не принят. Сказать игроку «неверный ПИН-код» значило бы соврать.
        var handler = new StubHandler(HttpStatusCode.Unauthorized, new StringContent(string.Empty));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(handler).SignInWithPinAsync("+992900000007", "123456", CancellationToken.None));
    }

    [Fact]
    public async Task RedeemingAClaim_GoesToThatClaim()
    {
        var claimId = Guid.NewGuid();
        var handler = new StubHandler(HttpStatusCode.OK, JsonContent.Create(Session()));

        var outcome = await Client(handler).RedeemClaimAsync(claimId, CancellationToken.None);

        Assert.NotNull(outcome.Session);
        Assert.Equal($"/api/devices/{Options.DeviceId:D}/sign-in-claims/{claimId:D}/redeem", handler.Request!.RequestUri!.AbsolutePath);
    }

    private static HttpPlayerSignInClient Client(StubHandler handler) => new(
        new TestHttpClientFactory(new HttpClient(handler)),
        Microsoft.Extensions.Options.Options.Create(Options),
        new InMemoryDeviceCredentialStore("rotated-secret"));

    private static PlatformPersonSessionResponse Session() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Фарход", true, "access-token", DateTimeOffset.UtcNow.AddMinutes(15),
        "refresh-token", DateTimeOffset.UtcNow.AddHours(12), Guid.NewGuid(), "ru", true);

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    private sealed class StubHandler(HttpStatusCode status, HttpContent content) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // Тело читается сейчас: после ответа клиент освободит запрос вместе с ним.
            Body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Request = request;
            return new HttpResponseMessage(status) { Content = content };
        }
    }
}
