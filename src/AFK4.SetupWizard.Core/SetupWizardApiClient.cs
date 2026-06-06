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

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Response body did not contain {typeof(T).Name}.");
    }
}
