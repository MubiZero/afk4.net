using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Operator.App.Shifts;

public sealed class HttpOperatorShiftApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorShiftApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<ShiftDto> OpenShiftAsync(
        Guid branchId,
        OpenShiftRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<ShiftDto, OpenShiftRequest>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/shifts/open",
            request,
            cancellationToken);
    }

    public async Task<ShiftDto?> GetCurrentShiftAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"/api/branches/{branchId:D}/shifts/current",
            cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent)
        {
            return null;
        }

        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadFromJsonAsync<ShiftDto>(JsonOptions, cancellationToken);
    }

    public Task<CashMovementDto> RecordCashMovementAsync(
        Guid shiftId,
        RecordCashMovementRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<CashMovementDto, RecordCashMovementRequest>(
            HttpMethod.Post,
            $"/api/shifts/{shiftId:D}/cash-movements",
            request,
            cancellationToken);
    }

    public Task<ShiftDto> CloseShiftAsync(
        Guid shiftId,
        CloseShiftRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<ShiftDto, CloseShiftRequest>(
            HttpMethod.Post,
            $"/api/shifts/{shiftId:D}/close",
            request,
            cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse, TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, requestUri, cancellationToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Platform API returned an empty shift response.");
    }

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new HttpRequestException(
            $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
            inner: null,
            response.StatusCode);
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(
        HttpMethod method,
        string requestUri,
        CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            throw new InvalidOperationException("Operator access token is missing.");
        }

        var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
