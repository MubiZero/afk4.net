using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Operator.App.Audit;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Audit;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorAuditApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid AuditRecordId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task SearchAsync_SendsBearerTokenAndQueryFilters()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(new AuditSearchResultDto(
            [
                new AuditRecordDto(
                    AuditRecordId,
                    OrganizationId,
                    BranchId,
                    ActorStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                    "sessions.start",
                    "Session",
                    "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb",
                    "Succeeded",
                    "PlatformApi",
                    "{}",
                    DateTimeOffset.Parse("2026-05-14T09:00:00Z"))
            ],
            Limit: 25)));
        var client = CreateClient(handler);

        var result = await client.SearchAsync(
            new OperatorAuditSearchRequest(
                BranchId,
                Action: "sessions.start",
                Outcome: "Succeeded",
                TargetType: "Session",
                FromUtc: DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                ToUtc: null,
                Limit: 25),
            CancellationToken.None);

        Assert.Single(result.Records);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.StartsWith($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/audit?", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Contains("action=sessions.start", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Contains("outcome=Succeeded", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Contains("targetType=Session", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Contains("fromUtc=", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Contains("limit=25", handler.LastPathAndQuery, StringComparison.Ordinal);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    private static HttpOperatorAuditApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        };

        return new HttpOperatorAuditApiClient(httpClient, new StaticOperatorTokenStore());
    }

    private static HttpResponseMessage JsonResponse<T>(T body)
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

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;

            return Task.FromResult(responder(request));
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
                DisplayName: "Auditor One",
                AccessToken: "staff-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-12T01:00:00Z"),
                RefreshToken: "refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-13T00:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
