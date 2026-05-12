using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Operator.App.Devices;

public sealed class HttpOperatorDeviceApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorDeviceApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<DeviceEnrollmentCodeDto> CreateEnrollmentCodeAsync(
        Guid branchId,
        Guid organizationId,
        int expiresInSeconds,
        CancellationToken cancellationToken)
    {
        return await SendAsync<DeviceEnrollmentCodeDto>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/device-enrollment-codes",
            new CreateDeviceEnrollmentCodeRequest(organizationId, expiresInSeconds),
            cancellationToken);
    }

    public async Task<DeviceCommandDto> DispatchDeviceCommandAsync(
        Guid deviceId,
        string type,
        IReadOnlyDictionary<string, string> payload,
        CancellationToken cancellationToken)
    {
        return await SendAsync<DeviceCommandDto>(
            HttpMethod.Post,
            $"/api/devices/{deviceId:D}/commands",
            new DispatchDeviceCommandRequest(type, payload),
            cancellationToken);
    }

    public async Task<DeviceCommandStatusDto> GetDeviceCommandStatusAsync(
        Guid deviceId,
        Guid commandId,
        CancellationToken cancellationToken)
    {
        return await SendAsync<DeviceCommandStatusDto>(
            HttpMethod.Get,
            $"/api/devices/{deviceId:D}/commands/{commandId:D}/status",
            body: null,
            cancellationToken);
    }

    public async Task<RotateDeviceCredentialResponse> RotateDeviceCredentialAsync(
        Guid deviceId,
        CancellationToken cancellationToken)
    {
        return await SendAsync<RotateDeviceCredentialResponse>(
            HttpMethod.Post,
            $"/api/devices/{deviceId:D}/credentials/rotate",
            body: null,
            cancellationToken);
    }

    public async Task<RevokeDeviceCredentialResponse> RevokeDeviceCredentialAsync(
        Guid deviceId,
        Guid credentialId,
        CancellationToken cancellationToken)
    {
        return await SendAsync<RevokeDeviceCredentialResponse>(
            HttpMethod.Post,
            $"/api/devices/{deviceId:D}/credentials/{credentialId:D}/revoke",
            body: null,
            cancellationToken);
    }

    private async Task<T> SendAsync<T>(
        HttpMethod method,
        string path,
        object? body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Platform API returned an empty response.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string path,
        CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            throw new InvalidOperationException("Operator access token is missing.");
        }

        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
