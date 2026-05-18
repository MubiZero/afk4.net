using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.GamingPc.Setup.Core;

public sealed class SetupApiClient(HttpClient httpClient) : ISetupApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task CheckHealthAsync(CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync("api/health", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<StaffSignInResponse> SignInAsync(
        string userName,
        string password,
        CancellationToken cancellationToken)
    {
        var request = new StaffSignInRequest(
            StagingSetupDefaults.OrganizationId,
            userName,
            password);

        using var response = await httpClient.PostAsJsonAsync(
            "api/auth/staff/sign-in",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<StaffSignInResponse>(response, cancellationToken);
    }

    public async Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var request = new CreateDeviceEnrollmentCodeRequest(
            StagingSetupDefaults.OrganizationId,
            ExpiresInSeconds: 300);

        using var message = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"api/branches/{StagingSetupDefaults.BranchId:D}/device-enrollment-codes",
            accessToken);
        message.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<DeviceEnrollmentCodeDto>(response, cancellationToken);
    }

    public async Task<DeviceEnrollmentResponse> EnrollDeviceAsync(
        string enrollmentCode,
        string machineName,
        CancellationToken cancellationToken)
    {
        var request = new DeviceEnrollmentRequest(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.BranchId,
            enrollmentCode,
            machineName,
            StagingSetupDefaults.AgentVersion,
            StagingSetupDefaults.ShellVersion,
            DateTimeOffset.UtcNow);

        using var response = await httpClient.PostAsJsonAsync(
            "api/devices/enroll",
            request,
            JsonOptions,
            cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<DeviceEnrollmentResponse>(response, cancellationToken);
    }

    public async Task<DeviceSeatAssignmentDto> AssignDeviceToSmokeSeatAsync(
        string accessToken,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        var request = new AssignDeviceSeatRequest(
            StagingSetupDefaults.OrganizationId,
            StagingSetupDefaults.SmokeSeatId);

        using var message = CreateAuthorizedRequest(
            HttpMethod.Post,
            $"api/branches/{StagingSetupDefaults.BranchId:D}/devices/{deviceId:D}/seat-assignment",
            accessToken);
        message.Content = JsonContent.Create(request, options: JsonOptions);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<DeviceSeatAssignmentDto>(response, cancellationToken);
    }

    public async Task<DeviceDetailDto> GetDeviceDetailAsync(
        string accessToken,
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        using var message = CreateAuthorizedRequest(HttpMethod.Get, $"api/devices/{deviceId:D}", accessToken);
        using var response = await httpClient.SendAsync(message, cancellationToken);

        response.EnsureSuccessStatusCode();
        return await ReadRequiredAsync<DeviceDetailDto>(response, cancellationToken);
    }

    private static HttpRequestMessage CreateAuthorizedRequest(HttpMethod method, string requestUri, string accessToken)
    {
        var message = new HttpRequestMessage(method, requestUri);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return message;
    }

    private static async Task<T> ReadRequiredAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"Response body did not contain {typeof(T).Name}.");
    }
}
