using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Tariffs;

namespace AFK4.SetupWizard.Core;

public static class SetupWizardDefaults
{
    // The wizard's FIRST discovery/enroll call goes here. After enroll the agent uses the
    // ApiBaseUrl the platform returns, so only this initial value is build-pinned. The build
    // injects the channel's platform origin via [AssemblyMetadata("AFK4.PlatformBaseUrl", ...)]
    // (see AFK4.SetupWizard.Core.csproj); dev/test builds omit it and fall back to staging.
    private const string StagingPlatformBaseUrl = "https://api.afk4.net";

    public static readonly Uri PlatformBaseUrl = new(ResolvePlatformBaseUrl(ReadInjectedBaseUrl()));

    public static string ResolvePlatformBaseUrl(string? injected)
    {
        var candidate = string.IsNullOrWhiteSpace(injected) ? StagingPlatformBaseUrl : injected.Trim();
        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"AFK4.PlatformBaseUrl is not a valid absolute http(s) URL: '{candidate}'.");
        }

        return candidate;
    }

    private static string? ReadInjectedBaseUrl()
        => typeof(SetupWizardDefaults).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(attribute => attribute.Key == "AFK4.PlatformBaseUrl")?.Value;
}

public sealed class SetupWizardApiClient(HttpClient httpClient) : ISetupWizardApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = httpClient;

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
        Guid organizationId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, InstallRoutes.AuthenticatedDiscover(organizationId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<InstallDiscoverResponse>(response, cancellationToken);
    }

    public async Task<InstallCreateSeatResponse> CreateSeatAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        Guid branchId,
        Guid zoneId,
        string name,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, InstallRoutes.AuthenticatedSeats(organizationId))
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

    public async Task UpdateBrandingAsync(
        Guid organizationId,
        string accessToken,
        string? logoUrl,
        string? accentColor,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, BrandingRoutes.Organization(organizationId))
        {
            Content = JsonContent.Create(
                new UpdateOrganizationBrandingRequest(logoUrl, accentColor), options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
    }

    public async Task<StaffInviteDto> InviteStaffAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string displayName,
        string phoneNumber,
        string roleName,
        CancellationToken cancellationToken)
    {
        // Логин сотрудника — его же номер в цифрах: на установке лишнее поле стоит дороже, чем
        // красивый логин, а вход по телефону в системе и так есть.
        var userName = new string(phoneNumber.Where(char.IsDigit).ToArray());

        using var request = new HttpRequestMessage(HttpMethod.Post, StaffRoutes.Invites(organizationId, branchId))
        {
            Content = JsonContent.Create(
                new CreateStaffInviteRequest(organizationId, userName, displayName, phoneNumber, null, [roleName]),
                options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StaffInviteDto>(response, cancellationToken);
    }

    public async Task<TariffDto> CreateTariffAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string name,
        long pricePerHourMinorUnits,
        CancellationToken cancellationToken)
    {
        var tariff = await SendAsync<CreateTariffRequest, TariffDto>(
            TariffRoutes.Tariffs(organizationId, branchId),
            new CreateTariffRequest(organizationId, name, Guid.NewGuid().ToString("N")),
            accessToken,
            cancellationToken);

        // Цена в системе живёт за минуту, а называют её за час — как и в панели управляющего.
        // Минимум в одну минорную единицу: бесплатный тариф заводится снятием с продажи, а не нулём.
        var pricePerMinute = Math.Max(1, (long)Math.Round(pricePerHourMinorUnits / 60d));

        await SendAsync<CreateTariffVersionRequest, TariffVersionDto>(
            TariffRoutes.Versions(organizationId, branchId, tariff.TariffId),
            new CreateTariffVersionRequest(
                organizationId,
                tariff.TariffId,
                CurrencyCode: "TJS",
                PricePerMinuteMinorUnits: pricePerMinute,
                MinimumBillableMinutes: 1,
                RoundingIncrementMinutes: 1,
                EffectiveFromUtc: DateTimeOffset.UtcNow,
                IdempotencyKey: Guid.NewGuid().ToString("N")),
            accessToken,
            cancellationToken);

        return tariff;
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, options: JsonOptions),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<TResponse>(response, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, InstallRoutes.AuthenticatedEnroll(organizationId))
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
