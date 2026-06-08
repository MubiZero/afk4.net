using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public static class SetupWizardDefaults
{
    public static readonly Uri PlatformBaseUrl = new("https://afk4.staging.mubi.dev");
}

public sealed class SetupWizardApiClient(HttpClient httpClient) : ISetupWizardApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = httpClient;

    public async Task<InstallDiscoverResponse> DiscoverAsync(string ownerCode, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/install/discover",
            new InstallDiscoverRequest(ownerCode),
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallDiscoverResponse>(response, cancellationToken);
    }

    public async Task<InstallCreateSeatResponse> CreateSeatAsync(
        string ownerCode,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/install/seats",
            new InstallCreateSeatRequest(ownerCode, branchId, zoneId, name),
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallCreateSeatResponse>(response, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollAsync(InstallEnrollRequest request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/install/enroll",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallEnrollResponse>(response, cancellationToken);
    }

    public async Task<StaffSignInResponse> SignInByPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/staff/sign-in-by-phone",
            new StaffSignInByPhoneRequest(phoneNumber, password),
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StaffSignInResponse>(response, cancellationToken);
    }

    public async Task<SetupWizardLoginResult> SignInByLoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/staff/sign-in-by-login",
            new StaffSignInByLoginRequest(login, password),
            JsonOptions,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var choose = await ReadRequiredAsync<StaffSignInChooseClubResponse>(response, cancellationToken);
            return new SetupWizardLoginResult(null, choose.Clubs);
        }

        response.EnsureSuccessStatusCode();
        var signedIn = await ReadRequiredAsync<StaffSignInResponse>(response, cancellationToken);
        return new SetupWizardLoginResult(signedIn, []);
    }

    public async Task<StaffSignInResponse> SignInToClubAsync(
        Guid organizationId,
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/staff/sign-in",
            new StaffSignInRequest(organizationId, login, password),
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StaffSignInResponse>(response, cancellationToken);
    }

    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest(userNameOrEmail),
            cancellationToken);

    public Task ResetPasswordByEmailAsync(
        string userNameOrEmail,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/reset-password",
            new StaffResetPasswordRequest(userNameOrEmail, code, newPassword),
            cancellationToken);

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(phoneNumber),
            cancellationToken);

    public Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(phoneNumber, code, newPassword),
            cancellationToken);

    public async Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/discover");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallDiscoverResponse>(response, cancellationToken);
    }

    public async Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/seats")
        {
            Content = JsonContent.Create(
                new AuthenticatedInstallCreateSeatRequest(branchId, zoneId, name),
                options: JsonOptions)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallCreateSeatResponse>(response, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "api/install/auth/enroll")
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallEnrollResponse>(response, cancellationToken);
    }

    // Reset endpoints return 200 on success (no token to persist). On a non-2xx, the body is
    // { "error": "<code>", "remainingAttempts": <n>? } — preserve both so the inline SMS reset
    // UI can show the specific reason and the attempts left (parity with the operator screen).
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

        throw new SetupWizardApiException(
            code,
            $"Platform API returned {(int)response.StatusCode} for {path}.",
            remainingAttempts);
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Response body did not contain {typeof(T).Name}.");
    }
}
