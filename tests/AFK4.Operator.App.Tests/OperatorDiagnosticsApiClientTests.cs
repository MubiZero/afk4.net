using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Diagnostics;
using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorDiagnosticsApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public async Task GetDiagnosticsAsync_SendsBearerTokenAndReadsDiagnostics()
    {
        var handler = new RecordingHttpMessageHandler(_ => JsonResponse(CreateDiagnostics()));
        var client = CreateClient(handler);

        var result = await client.GetDiagnosticsAsync(BranchId, CancellationToken.None);

        Assert.Equal(BranchId, result.BranchId);
        Assert.Equal(12, result.DeviceSummary.TotalDevices);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/branches/{BranchId:D}/diagnostics", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
    }

    private static HttpOperatorDiagnosticsApiClient CreateClient(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        };

        return new HttpOperatorDiagnosticsApiClient(httpClient, new StaticOperatorTokenStore());
    }

    private static BranchDiagnosticsDto CreateDiagnostics()
    {
        return new BranchDiagnosticsDto(
            OrganizationId,
            BranchId,
            DateTimeOffset.Parse("2026-05-16T10:00:00Z"),
            new DeviceDiagnosticsSummaryDto(12, 10, 8, 2, 300, DateTimeOffset.Parse("2026-05-16T09:59:00Z")),
            new CommandDiagnosticsSummaryDto(3, 1, []),
            new UpdateDiagnosticsSummaryDto(2, 1, 1, 0, []),
            []);
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
        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OperatorTokenSnapshot?>(new OperatorTokenSnapshot(
                StaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                OrganizationId: OrganizationId,
                DisplayName: "Technician One",
                AccessToken: "staff-access-token",
                AccessTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-16T11:00:00Z"),
                RefreshToken: "refresh-token",
                RefreshTokenExpiresAtUtc: DateTimeOffset.Parse("2026-05-17T10:00:00Z")));
        }

        public Task ClearAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
