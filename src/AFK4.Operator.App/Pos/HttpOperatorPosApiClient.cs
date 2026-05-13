using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Pos;
using AFK4.Shared.Contracts.Receipts;

namespace AFK4.Operator.App.Pos;

public sealed class HttpOperatorPosApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorPosApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<IReadOnlyList<PosProductDto>> GetCatalogAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        return GetAsync<IReadOnlyList<PosProductDto>>(
            $"/api/branches/{branchId:D}/pos/catalog",
            cancellationToken);
    }

    public Task<PosSaleDto> CreateSaleAsync(
        Guid branchId,
        CreatePosSaleRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<PosSaleDto, CreatePosSaleRequest>(
            HttpMethod.Post,
            $"/api/branches/{branchId:D}/pos/sales",
            request,
            cancellationToken);
    }

    public Task<PosSaleDto> PaySaleManualAsync(
        Guid saleId,
        ManualPaymentRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<PosSaleDto, ManualPaymentRequest>(
            HttpMethod.Post,
            $"/api/pos/sales/{saleId:D}/payments/manual",
            request,
            cancellationToken);
    }

    public Task<PosSaleDto> RefundSaleAsync(
        Guid saleId,
        RefundPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<PosSaleDto, RefundPosSaleRequest>(
            HttpMethod.Post,
            $"/api/pos/sales/{saleId:D}/refunds",
            request,
            cancellationToken);
    }

    public Task<PosSaleDto> VoidSaleAsync(
        Guid saleId,
        VoidPosSaleRequest request,
        CancellationToken cancellationToken)
    {
        return SendAsync<PosSaleDto, VoidPosSaleRequest>(
            HttpMethod.Post,
            $"/api/pos/sales/{saleId:D}/void",
            request,
            cancellationToken);
    }

    public Task<PosSaleDto> GetSaleAsync(
        Guid saleId,
        CancellationToken cancellationToken)
    {
        return GetAsync<PosSaleDto>(
            $"/api/pos/sales/{saleId:D}",
            cancellationToken);
    }

    public Task<ReceiptDto> GetReceiptAsync(
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        return GetAsync<ReceiptDto>(
            $"/api/receipts/{receiptId:D}",
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
        return body ?? throw new InvalidOperationException("Platform API returned an empty POS response.");
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
