using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Payments.DcGate;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public sealed class DcGateAdminClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest;
        public string? LastBody;
        private readonly HttpStatusCode status;
        private readonly string body;

        public StubHandler(HttpStatusCode status, string body)
        {
            this.status = status;
            this.body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status)
            {
                Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
            };
        }
    }

    private static DcGateAdminClient Create(StubHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://dcgate.test") }, "admin-secret-123");

    [Fact]
    public async Task CreateProjectAsync_sends_admin_secret_and_parses_apikey_and_secret()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"proj_1","status":"pending_telegram","cardLast4":"4242","apiKey":"key_live","webhookSecret":"whsec_abc"}""");
        var client = Create(handler);

        var result = await client.CreateProjectAsync(
            new DcGateCreateProjectRequest("AFK4 / Org / Branch", "4111111111114242",
                "https://afk4.test/api/public/payments/dcgate/webhook", 30, "11111111-1111-1111-1111-111111111111"),
            CancellationToken.None);

        Assert.Equal("proj_1", result.Id);
        Assert.Equal("4242", result.CardLast4);
        Assert.Equal("key_live", result.ApiKey);
        Assert.Equal("whsec_abc", result.WebhookSecret);
        Assert.False(result.IdempotentReplay);
        Assert.Equal("admin-secret-123", handler.LastRequest!.Headers.GetValues("x-admin-secret").Single());
        Assert.Contains("\"externalId\":\"11111111-1111-1111-1111-111111111111\"", handler.LastBody);
    }

    [Fact]
    public async Task CreateProjectAsync_marks_idempotent_replay_when_apikey_absent()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"id":"proj_1","status":"pending_telegram","cardLast4":"4242","idempotentReplay":true}""");
        var client = Create(handler);

        var result = await client.CreateProjectAsync(
            new DcGateCreateProjectRequest("n", "4111111111114242", "https://afk4.test/wh", 30, "x"),
            CancellationToken.None);

        Assert.True(result.IdempotentReplay);
        Assert.Null(result.ApiKey);
    }

    [Fact]
    public async Task StartTelegramAsync_posts_phone_and_parses_attempt()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"loginAttemptId":"att_9","state":"code_required"}""");
        var client = Create(handler);

        var result = await client.StartTelegramAsync("proj_1", "+992900000000", CancellationToken.None);

        Assert.Equal("att_9", result.LoginAttemptId);
        Assert.Equal("code_required", result.State);
        Assert.Equal("/api/admin/projects/proj_1/telegram-session/start", handler.LastRequest!.RequestUri!.AbsolutePath);
    }

    [Fact]
    public async Task GetStatusAsync_parses_session_health()
    {
        var handler = new StubHandler(HttpStatusCode.OK,
            """{"sessionHealth":"online","lastConnectedAt":"2026-06-04T10:00:00Z","lastMessageAt":null,"telegramMessagesCount":7}""");
        var client = Create(handler);

        var result = await client.GetStatusAsync("proj_1", CancellationToken.None);

        Assert.Equal("online", result.SessionHealth);
        Assert.Equal(7, result.TelegramMessagesCount);
    }

    [Fact]
    public async Task CreateProjectAsync_throws_with_dcgate_message_on_4xx()
    {
        var handler = new StubHandler(HttpStatusCode.BadRequest, """{"message":"card already in use"}""");
        var client = Create(handler);

        var ex = await Assert.ThrowsAsync<DcGateAdminException>(() =>
            client.CreateProjectAsync(new DcGateCreateProjectRequest("n", "4111", "wh", 30, "x"), CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadRequest, ex.StatusCode);
        Assert.Contains("card already in use", ex.Message);
    }
}
