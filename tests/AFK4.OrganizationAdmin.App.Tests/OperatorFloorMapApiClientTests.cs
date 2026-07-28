using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.OrganizationAdmin.App.FloorMap;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OperatorFloorMapApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public async Task GetFloorMapAsync_SendsBearerTokenToBranchFloorMapEndpoint()
    {
        var response = new FloorMapDto(BranchId, "Demo Branch", []);
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(response));
        var client = new HttpOperatorFloorMapApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, new StaticOrganizationAdminTokenStore());

        var result = await client.GetFloorMapAsync(BranchId, CancellationToken.None);

        Assert.Equal("Demo Branch", result.BranchName);
        Assert.Equal(HttpMethod.Get, handler.LastMethod);
        Assert.Equal($"/api/organizations/{OrganizationId:D}/branches/{BranchId:D}/floor-map", handler.LastPathAndQuery);
        Assert.Equal(new AuthenticationHeaderValue("Bearer", "staff-access-token"), handler.LastAuthorization);
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

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastMethod = request.Method;
            LastPathAndQuery = request.RequestUri?.PathAndQuery;
            LastAuthorization = request.Headers.Authorization;

            return Task.FromResult(responder(request));
        }
    }

    private sealed class StaticOrganizationAdminTokenStore : IOrganizationAdminTokenStore
    {
        public Task SaveAsync(OrganizationAdminTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public Task<OrganizationAdminTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult<OrganizationAdminTokenSnapshot?>(new OrganizationAdminTokenSnapshot(
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
