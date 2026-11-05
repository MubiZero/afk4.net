using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.Shared.Contracts.Media;

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

public sealed class SetupWizardApiClient(HttpClient httpClient) : ISetupWizardApiClient, IInstallCodeEnrollmentClient
{
    private readonly HttpClient httpClient = httpClient;

    public async Task<StaffSignInResponse> SignInByPhoneAsync(
        string phoneNumber,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            StaffAuthRoutes.SignInByPhone,
            new StaffSignInByPhoneRequest(phoneNumber, password),
            SetupWizardApiJson.Default.StaffSignInByPhoneRequest,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.StaffSignInResponse, cancellationToken);
    }

    public async Task<SetupWizardLoginResult> SignInByLoginAsync(
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            StaffAuthRoutes.SignInByLogin,
            new StaffSignInByLoginRequest(login, password),
            SetupWizardApiJson.Default.StaffSignInByLoginRequest,
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.Conflict)
        {
            var choose = await ReadRequiredAsync(response, SetupWizardApiJson.Default.StaffSignInChooseClubResponse, cancellationToken);
            return new SetupWizardLoginResult(null, choose.Clubs);
        }

        await EnsureSuccessAsync(response, cancellationToken);
        var signedIn = await ReadRequiredAsync(response, SetupWizardApiJson.Default.StaffSignInResponse, cancellationToken);
        return new SetupWizardLoginResult(signedIn, []);
    }

    public async Task<StaffSignInResponse> SignInToClubAsync(
        Guid organizationId,
        string login,
        string password,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(
            StaffAuthRoutes.SignIn,
            new StaffSignInRequest(organizationId, login, password),
            SetupWizardApiJson.Default.StaffSignInRequest,
            cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.StaffSignInResponse, cancellationToken);
    }

    public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/forgot-password",
            new StaffForgotPasswordRequest(userNameOrEmail),
            SetupWizardApiJson.Default.StaffForgotPasswordRequest,
            cancellationToken);

    public Task ResetPasswordByEmailAsync(
        string userNameOrEmail,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/reset-password",
            new StaffResetPasswordRequest(userNameOrEmail, code, newPassword),
            SetupWizardApiJson.Default.StaffResetPasswordRequest,
            cancellationToken);

    public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/forgot-password-by-phone",
            new StaffForgotPasswordByPhoneRequest(phoneNumber),
            SetupWizardApiJson.Default.StaffForgotPasswordByPhoneRequest,
            cancellationToken);

    public Task ResetPasswordByPhoneAsync(
        string phoneNumber,
        string code,
        string newPassword,
        CancellationToken cancellationToken)
        => PostResetAsync(
            "api/auth/staff/reset-password-by-phone",
            new StaffResetPasswordByPhoneRequest(phoneNumber, code, newPassword),
            SetupWizardApiJson.Default.StaffResetPasswordByPhoneRequest,
            cancellationToken);

    public async Task<InstallDiscoverResponse> DiscoverAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, InstallRoutes.AuthenticatedDiscover(organizationId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.InstallDiscoverResponse, cancellationToken);
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
                SetupWizardApiJson.Default.AuthenticatedInstallCreateSeatRequest)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.InstallCreateSeatResponse, cancellationToken);
    }

    public async Task UpdateBrandingAsync(
        Guid organizationId,
        string accessToken,
        string? logoUrl,
        string? accentColor,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PATCH"), BrandingRoutes.Organization(organizationId))
        {
            Content = JsonContent.Create(
                new UpdateOrganizationBrandingRequest(logoUrl, accentColor), SetupWizardApiJson.Default.UpdateOrganizationBrandingRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
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
                SetupWizardApiJson.Default.CreateStaffInviteRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.StaffInviteDto, cancellationToken);
    }

    public async Task<UploadedMediaDto> UploadOrganizationLogoAsync(
        Guid organizationId,
        Guid branchId,
        string accessToken,
        string filePath,
        CancellationToken cancellationToken)
    {
        // Файл читается потоком: картинка клуба мелкая, но держать её целиком в памяти незачем,
        // а размер и тип проверит сервер — он же и отклонит всё, что не картинка.
        using var file = File.OpenRead(filePath);
        using var content = new MultipartFormDataContent
        {
            { new StringContent(MediaPurposeNames.OrganizationLogo), "purpose" },
            { new StreamContent(file), "file", Path.GetFileName(filePath) },
        };

        using var request = new HttpRequestMessage(
            HttpMethod.Post, MediaRoutes.BranchMedia(organizationId, branchId))
        {
            Content = content,
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.UploadedMediaDto, cancellationToken);
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
            SetupWizardApiJson.Default.CreateTariffRequest,
            SetupWizardApiJson.Default.TariffDto,
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
            SetupWizardApiJson.Default.CreateTariffVersionRequest,
            SetupWizardApiJson.Default.TariffVersionDto,
            accessToken,
            cancellationToken);

        return tariff;
    }

    private async Task<TResponse> SendAsync<TRequest, TResponse>(
        string path,
        TRequest body,
        JsonTypeInfo<TRequest> requestInfo,
        JsonTypeInfo<TResponse> responseInfo,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(body, requestInfo),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, responseInfo, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollAuthenticatedAsync(
        Guid organizationId,
        string accessToken,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, InstallRoutes.AuthenticatedEnroll(organizationId))
        {
            Content = JsonContent.Create(request, SetupWizardApiJson.Default.AuthenticatedInstallEnrollRequest)
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await httpClient.SendAsync(httpRequest, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.InstallEnrollResponse, cancellationToken);
    }

    public async Task<InstallEnrollResponse> EnrollByCodeAsync(
        InstallCodeEnrollRequest request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(InstallRoutes.CodeEnroll, request, SetupWizardApiJson.Default.InstallCodeEnrollRequest, cancellationToken);

        await EnsureSuccessAsync(response, cancellationToken);
        return await ReadRequiredAsync(response, SetupWizardApiJson.Default.InstallEnrollResponse, cancellationToken);
    }

    // Reset endpoints return 200 on success (no token to persist). On a non-2xx, the body is
    // { "error": "<code>", "remainingAttempts": <n>? } — preserve both so the inline SMS reset
    // UI can show the specific reason and the attempts left (parity with the operator screen).
    private async Task PostResetAsync<T>(
        string path, T body, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        using var response = await httpClient.PostAsJsonAsync(path, body, typeInfo, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var failure = ReadFailure(errorBody);

        throw new SetupWizardApiException(
            failure.Code ?? "reset_failed",
            $"Platform API returned {(int)response.StatusCode} for {path}.",
            failure.RemainingAttempts);
    }

    /// <summary>
    /// Отказ с машинным кодом доезжает до мастера кодом: только по нему экран назовёт причину на
    /// языке того, кто её читает, — английскую фразу сервера показать нельзя. Ответ без кода
    /// остаётся обычной HTTP-ошибкой: придумывать код за сервер мы не станем.
    /// </summary>
    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var failure = ReadFailure(body);
        if (failure.Code is not null)
        {
            var path = response.RequestMessage?.RequestUri?.PathAndQuery ?? "the platform API";
            throw new SetupWizardApiException(
                failure.Code,
                $"Platform API returned {(int)response.StatusCode} for {path}.",
                failure.RemainingAttempts);
        }

        throw HttpFailure.For(response.StatusCode, $"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}).");
    }

    private readonly record struct ApiFailure(string? Code, int? RemainingAttempts);

    /// <summary>
    /// Разбор тела отказа. Код лежит либо в <c>code</c>, либо — в кассе, складе и сбросе пароля —
    /// прямо в <c>error</c>; свободный текст («Tariff name is required.») кодом не считаем, иначе
    /// английская фраза уехала бы в интерфейс под видом машинного имени.
    /// </summary>
    private static ApiFailure ReadFailure(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            string? code = null;
            foreach (var property in new[] { "code", "error" })
            {
                if (document.RootElement.TryGetProperty(property, out var element)
                    && element.ValueKind == JsonValueKind.String
                    && LooksLikeCode(element.GetString()))
                {
                    code = element.GetString();
                    break;
                }
            }

            int? remainingAttempts = null;
            if (document.RootElement.TryGetProperty("remainingAttempts", out var remainingElement)
                && remainingElement.ValueKind == JsonValueKind.Number)
            {
                remainingAttempts = remainingElement.GetInt32();
            }

            return new ApiFailure(code, remainingAttempts);
        }
        catch (JsonException)
        {
            return new ApiFailure(null, null);
        }
    }

    private static bool LooksLikeCode(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.All(character => char.IsAsciiLetterLower(character) || character == '_' || char.IsAsciiDigit(character));

    private static async Task<T> ReadRequiredAsync<T>(
        HttpResponseMessage response, JsonTypeInfo<T> typeInfo, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync(typeInfo, cancellationToken);
        return value ?? throw new InvalidOperationException($"Response body did not contain {typeof(T).Name}.");
    }
}
