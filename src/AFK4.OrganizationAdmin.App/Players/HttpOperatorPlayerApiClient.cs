using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Packages;

namespace AFK4.OrganizationAdmin.App.Players;

public sealed class HttpOperatorPlayerApiClient(HttpClient httpClient, IOrganizationAdminTokenStore tokenStore) : IOperatorPlayerApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<PlayerSearchResultDto>> SearchPlayersAsync(
        Guid branchId,
        string query,
        int limit,
        CancellationToken cancellationToken)
    {
        var encodedQuery = Uri.EscapeDataString(query);
        return GetAsync<IReadOnlyList<PlayerSearchResultDto>>(
            $"branches/{branchId:D}/players?query={encodedQuery}&limit={limit}",
            cancellationToken);
    }

    public Task<PlayerAccountDto> CreatePlayerAsync(
        Guid branchId,
        CreatePlayerAccountRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<PlayerAccountDto, CreatePlayerAccountRequest>(
            HttpMethod.Post,
            $"branches/{branchId:D}/players",
            request,
            cancellationToken);
    }

    public Task<WalletSummaryDto> GetWalletSummaryAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        return GetAsync<WalletSummaryDto>(
            $"players/{playerAccountId:D}/wallet-summary",
            cancellationToken);
    }

    public Task<IReadOnlyList<PlayerPackageDto>> GetPlayerPackagesAsync(
        Guid playerAccountId,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<PlayerPackageDto>>(
            $"players/{playerAccountId:D}/packages",
            cancellationToken);
    }

    public Task<WalletSummaryDto> TopUpWalletAsync(
        Guid playerAccountId,
        TopUpWalletRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<WalletSummaryDto, TopUpWalletRequest>(
            HttpMethod.Post,
            $"players/{playerAccountId:D}/wallet/top-ups",
            request,
            cancellationToken);
    }

    public Task<WalletSummaryDto> PayDebtAsync(
        Guid playerAccountId,
        PayDebtRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<WalletSummaryDto, PayDebtRequest>(
            HttpMethod.Post,
            $"players/{playerAccountId:D}/debts/payments",
            request,
            cancellationToken);
    }

    private async Task<TResponse> GetAsync<TResponse>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> SendAsync<TResponse, TRequest>(
        HttpMethod method,
        string requestUri,
        TRequest body,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(method, requestUri, cancellationToken);
        request.Content = JsonContent.Create(body, options: JsonOptions);
        return await SendAndReadAsync<TResponse>(request, cancellationToken);
    }

    private async Task<TResponse> SendAndReadAsync<TResponse>(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var body = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return body ?? throw new InvalidOperationException("Platform API returned an empty player response.");
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

        var request = new HttpRequestMessage(method, OrganizationApiRoute.Build(snapshot, requestUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
