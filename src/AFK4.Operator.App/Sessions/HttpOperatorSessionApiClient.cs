using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Sessions;

namespace AFK4.Operator.App.Sessions;

public sealed class HttpOperatorSessionApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorSessionApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<SessionCommandResponse> StartGuestSessionAsync(
        Guid branchId,
        StartGuestSessionRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/sessions/start",
            request,
            cancellationToken);
    }

    public Task<SessionCommandResponse> ExtendSessionAsync(
        Guid sessionId,
        ExtendSessionRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/sessions/{sessionId:D}/extend",
            request,
            cancellationToken);
    }

    public Task<SessionCommandResponse> TransferSessionAsync(
        Guid sessionId,
        TransferSessionRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/sessions/{sessionId:D}/transfer",
            request,
            cancellationToken);
    }

    public Task<SessionCommandResponse> EndSessionAsync(
        Guid sessionId,
        EndSessionRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync(
            HttpMethod.Post,
            $"/api/sessions/{sessionId:D}/end",
            request,
            cancellationToken);
    }

    private async Task<SessionCommandResponse> SendAsync<TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, requestUri, cancellationToken);
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

        var commandResponse = await response.Content.ReadFromJsonAsync<SessionCommandResponse>(
            JsonOptions,
            cancellationToken);
        return commandResponse ?? throw new InvalidOperationException("Platform API returned an empty session command response.");
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
