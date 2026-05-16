using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Updates;

namespace AFK4.Operator.App.Updates;

public sealed class HttpOperatorUpdateApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorUpdateApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IReadOnlyList<UpdateRolloutStatusDto>> GetRolloutStatusesAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"/api/branches/{branchId:D}/updates/rollouts",
            cancellationToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<IReadOnlyList<UpdateRolloutStatusDto>>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Platform API returned an empty update status response.");
    }

    public Task<UpdatePackageDto> RegisterPackageAsync(
        Guid branchId,
        CreateUpdatePackageRequest request,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync<CreateUpdatePackageRequest, UpdatePackageDto>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/updates/packages",
            request,
            "update package registration",
            cancellationToken);
    }

    public Task<UpdatePackageDto> ChangePackageStateAsync(
        Guid branchId,
        Guid updatePackageId,
        UpdatePackageStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync<UpdatePackageStateChangeRequest, UpdatePackageDto>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/updates/packages/{updatePackageId:D}/state",
            request,
            "update package state change",
            cancellationToken);
    }

    public Task<UpdateRolloutDto> CreateRolloutAsync(
        Guid branchId,
        CreateUpdateRolloutRequest request,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync<CreateUpdateRolloutRequest, UpdateRolloutDto>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/updates/rollouts",
            request,
            "update rollout creation",
            cancellationToken);
    }

    public Task<UpdateRolloutDto> ChangeRolloutStateAsync(
        Guid branchId,
        Guid updateRolloutId,
        UpdateRolloutStateChangeRequest request,
        CancellationToken cancellationToken)
    {
        return SendJsonAsync<UpdateRolloutStateChangeRequest, UpdateRolloutDto>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/updates/rollouts/{updateRolloutId:D}/state",
            request,
            "update rollout state change",
            cancellationToken);
    }

    private async Task<TResponse> SendJsonAsync<TRequest, TResponse>(
        HttpMethod method,
        string path,
        TRequest body,
        string operationName,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, path, cancellationToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException($"Platform API returned an empty {operationName} response.");
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
