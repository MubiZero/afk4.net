using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Sessions;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorSessionApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid SeatId = Guid.Parse("11111111-1111-4111-8111-111111111111");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-8444-444444444444");
    private static readonly Guid TargetSeatId = Guid.Parse("55555555-5555-4555-8555-555555555555");

    [Fact]
    public async Task StartGuestSessionAsync_PostsBearerAuthenticatedRequest()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateResponse()));
        var client = new HttpOperatorSessionApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
        var request = new StartGuestSessionRequest(
            OrganizationId,
            SeatId,
            60,
            "default",
            "session-start-001",
            BillingMode: BillingModeNames.PostpaidDebt);

        var response = await client.StartGuestSessionAsync(BranchId, request, CancellationToken.None);

        Assert.Equal(SessionId, response.Session.SessionId);
        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/sessions/start", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);

        var body = DeserializeRequest<StartGuestSessionRequest>(handler.LastRequestBody);
        Assert.Equal(SeatId, body.SeatId);
        Assert.Equal("session-start-001", body.IdempotencyKey);
    }

    [Fact]
    public async Task ExtendSessionAsync_PostsToSessionExtendEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateResponse()));
        var client = CreateClient(handler);
        var request = new ExtendSessionRequest(30, "default", "session-extend-001");

        await client.ExtendSessionAsync(SessionId, request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/sessions/{SessionId:D}/extend", handler.LastPathAndQuery);

        var body = DeserializeRequest<ExtendSessionRequest>(handler.LastRequestBody);
        Assert.Equal(30, body.AdditionalMinutes);
        Assert.Equal("session-extend-001", body.IdempotencyKey);
    }

    [Fact]
    public async Task TransferSessionAsync_PostsToSessionTransferEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateResponse()));
        var client = CreateClient(handler);
        var request = new TransferSessionRequest(TargetSeatId, "session-transfer-001");

        await client.TransferSessionAsync(SessionId, request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/sessions/{SessionId:D}/transfer", handler.LastPathAndQuery);

        var body = DeserializeRequest<TransferSessionRequest>(handler.LastRequestBody);
        Assert.Equal(TargetSeatId, body.TargetSeatId);
        Assert.Equal("session-transfer-001", body.IdempotencyKey);
    }

    [Fact]
    public async Task EndSessionAsync_PostsToSessionEndEndpoint()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(CreateResponse()));
        var client = CreateClient(handler);
        var request = new EndSessionRequest("operator close", "session-end-001");

        await client.EndSessionAsync(SessionId, request, CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal($"/api/sessions/{SessionId:D}/end", handler.LastPathAndQuery);

        var body = DeserializeRequest<EndSessionRequest>(handler.LastRequestBody);
        Assert.Equal("operator close", body.Reason);
        Assert.Equal("session-end-001", body.IdempotencyKey);
    }

    private static HttpOperatorSessionApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        return new HttpOperatorSessionApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOperatorTokenStore());
    }

    private static SessionCommandResponse CreateResponse()
    {
        return new SessionCommandResponse(
            "response-key",
            new SessionDto(
                SessionId,
                OrganizationId,
                BranchId,
                SeatId,
                Guid.Parse("33333333-3333-4333-8333-333333333333"),
                SessionStateNames.Active,
                "default",
                DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                null,
                3600,
                null),
            []);
    }

    private static T DeserializeRequest<T>(string? json)
    {
        Assert.False(string.IsNullOrWhiteSpace(json));
        var result = JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(result);
        return result;
    }

    private static HttpResponseMessage JsonContentResponse<T>(T body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(body)
        };
    }

    private sealed class RecordingHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public HttpMethod? LastMethod { get; private set; }

        public string? LastPathAndQuery { get; private set; }

        public string? LastRequestBody { get; private set; }

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return responder(request);
        }
    }

    private sealed class StaticOperatorTokenStore : IOperatorTokenStore
    {
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                OrganizationId: OrganizationId,
                DisplayName: "Cashier One",
                AccessToken: "staff-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                RefreshToken: "refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-15T10:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
