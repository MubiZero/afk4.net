using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorAuthApiClientTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public async Task SignInAsync_PostsCredentialsAndSavesTokenSnapshot()
    {
        var response = CreateAuthResponse(
            accessToken: "access-token",
            refreshToken: "refresh-token",
            permissions: [StaffPermissionNames.ViewFloorMap]);
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(response));
        var tokenStore = new RecordingOperatorTokenStore();
        var client = new HttpOperatorAuthApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, tokenStore);

        var result = await client.SignInAsync(
            OrganizationId,
            "cashier",
            "password",
            CancellationToken.None);

        Assert.Equal(HttpMethod.Post, handler.LastMethod);
        Assert.Equal("/api/auth/staff/sign-in", handler.LastPathAndQuery);
        Assert.Equal("access-token", tokenStore.SavedSnapshot?.AccessToken);
        Assert.Equal("refresh-token", tokenStore.SavedSnapshot?.RefreshToken);
        Assert.Equal(["cashier_operator"], tokenStore.SavedSnapshot?.RoleNames);
        Assert.Contains(StaffPermissionNames.ViewFloorMap, result.Permissions);

        var body = DeserializeRequest<StaffSignInRequest>(handler.LastRequestBody);
        Assert.Equal(OrganizationId, body.OrganizationId);
        Assert.Equal("cashier", body.UserName);
        Assert.Equal("password", body.Password);
    }

    [Fact]
    public async Task RefreshAsync_UsesStoredOrganizationAndSavesRotatedTokens()
    {
        var response = CreateAuthResponse(
            accessToken: "rotated-access-token",
            refreshToken: "rotated-refresh-token",
            permissions: [StaffPermissionNames.ViewFloorMap, StaffPermissionNames.ViewShift]);
        var handler = new RecordingHttpMessageHandler(_ => JsonContentResponse(response));
        var tokenStore = new RecordingOperatorTokenStore
        {
            SavedSnapshot = new OperatorTokenSnapshot(
                StaffUserId,
                OrganizationId,
                "Cashier One",
                "old-access-token",
                DateTimeOffset.Parse("2026-05-14T09:00:00Z"),
                "old-refresh-token",
                DateTimeOffset.Parse("2026-05-15T09:00:00Z"))
        };
        var client = new HttpOperatorAuthApiClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("http://localhost:5074")
        }, tokenStore);

        var result = await client.RefreshAsync("old-refresh-token", CancellationToken.None);

        Assert.Equal("/api/auth/staff/refresh", handler.LastPathAndQuery);
        Assert.Equal("rotated-access-token", tokenStore.SavedSnapshot?.AccessToken);
        Assert.Equal("rotated-refresh-token", tokenStore.SavedSnapshot?.RefreshToken);
        Assert.Contains(StaffPermissionNames.ViewShift, result.Permissions);

        var body = DeserializeRequest<StaffRefreshTokenRequest>(handler.LastRequestBody);
        Assert.Equal(OrganizationId, body.OrganizationId);
        Assert.Equal("old-refresh-token", body.RefreshToken);
    }

    private static StaffSignInResponse CreateAuthResponse(
        string accessToken,
        string refreshToken,
        IReadOnlyList<string> permissions)
    {
        return new StaffSignInResponse(
            StaffUserId,
            OrganizationId,
            "Cashier One",
            accessToken,
            DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
            refreshToken,
            DateTimeOffset.Parse("2026-05-15T10:00:00Z"),
            [BranchId],
            permissions)
        {
            RoleNames = ["cashier_operator"]
        };
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

        public AuthenticationHeaderValue? LastAuthorization { get; private set; }

        public string? LastRequestBody { get; private set; }

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

    private sealed class RecordingOperatorTokenStore : IOperatorTokenStore
    {
        public OperatorTokenSnapshot? SavedSnapshot { get; set; }

        public Task SaveAsync(OperatorTokenSnapshot snapshot, CancellationToken cancellationToken)
        {
            SavedSnapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<OperatorTokenSnapshot?> LoadAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(SavedSnapshot);
        }

        public Task ClearAsync(CancellationToken cancellationToken)
        {
            SavedSnapshot = null;
            return Task.CompletedTask;
        }
    }
}
