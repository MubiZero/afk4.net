using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Auth;

public sealed class HttpOperatorAuthApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorAuthApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<StaffSignInResponse> SignInAsync(
        Guid organizationId,
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var response = await SendAsync(
            "/api/auth/staff/sign-in",
            new StaffSignInRequest(organizationId, userName, password),
            cancellationToken);

        await SaveSnapshotAsync(response, cancellationToken);
        return response;
    }

    public async Task<StaffSignInResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null)
        {
            throw new InvalidOperationException("Operator refresh token is missing.");
        }

        var response = await SendAsync(
            "/api/auth/staff/refresh",
            new StaffRefreshTokenRequest(snapshot.OrganizationId, refreshToken),
            cancellationToken);

        await SaveSnapshotAsync(response, cancellationToken);
        return response;
    }

    private async Task<StaffSignInResponse> SendAsync<T>(
        string path,
        T body,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<StaffSignInResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Platform API returned an empty auth response.");
    }

    private Task SaveSnapshotAsync(StaffSignInResponse response, CancellationToken cancellationToken)
    {
        return tokenStore.SaveAsync(
            new OperatorTokenSnapshot(
                response.StaffUserId,
                response.OrganizationId,
                response.DisplayName,
                response.AccessToken,
                response.AccessTokenExpiresAtUtc,
                response.RefreshToken,
                response.RefreshTokenExpiresAtUtc)
            {
                BranchIds = response.BranchIds,
                Permissions = response.Permissions
            },
            cancellationToken);
    }
}
