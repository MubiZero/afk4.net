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

    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest(userNameOrEmail),
            cancellationToken);

    public Task ResetPasswordByEmailAsync(
        string userNameOrEmail,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/reset-password",
            new StaffResetPasswordRequest(userNameOrEmail, code, newPassword),
            cancellationToken);

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(phoneNumber),
            cancellationToken);

    public Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "/api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(phoneNumber, code, newPassword),
            cancellationToken);

    // Reset endpoints return 200 on success (no token to persist). On a non-2xx, the body is
    // { "error": "<code>", "remainingAttempts": <n>? } — preserve both so the UI can show the
    // specific reason and the attempts left (parity with the Platform.Web reset screen).
    private async Task PostResetAsync<T>(string path, T body, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, JsonOptions, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var code = "reset_failed";
        int? remainingAttempts = null;
        try
        {
            using var document = JsonDocument.Parse(errorBody);
            if (document.RootElement.TryGetProperty("error", out var errorElement)
                && errorElement.ValueKind == JsonValueKind.String)
            {
                code = errorElement.GetString() ?? code;
            }

            if (document.RootElement.TryGetProperty("remainingAttempts", out var remainingElement)
                && remainingElement.ValueKind == JsonValueKind.Number)
            {
                remainingAttempts = remainingElement.GetInt32();
            }
        }
        catch (JsonException)
        {
            // Non-JSON error body: keep the generic code.
        }

        throw new OperatorAuthApiException(
            code,
            $"Platform API returned {(int)response.StatusCode} for {path}.",
            remainingAttempts);
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
                RoleNames = response.RoleNames,
                BranchIds = response.BranchIds,
                Permissions = response.Permissions
            },
            cancellationToken);
    }
}
