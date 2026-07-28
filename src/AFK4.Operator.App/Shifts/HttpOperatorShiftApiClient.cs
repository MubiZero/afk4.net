using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Reports;
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
            $"branches/{branchId:D}/shifts/open",
            request,
            cancellationToken);
    }

    public async Task<ShiftDto?> GetCurrentShiftAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(
            HttpMethod.Get,
            $"branches/{branchId:D}/shifts/current",
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
            $"shifts/{shiftId:D}/cash-movements",
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
            $"shifts/{shiftId:D}/close",
            request,
            cancellationToken);
    }

    public Task<ShiftReportResultDto> GetShiftReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetAsync<ShiftReportResultDto>(
            BuildReportUri($"branches/{branchId:D}/reports/shifts", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<SalesReportResultDto> GetSalesReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetAsync<SalesReportResultDto>(
            BuildReportUri($"branches/{branchId:D}/reports/sales", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<GameplayTimeReportResultDto> GetGameplayTimeReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetAsync<GameplayTimeReportResultDto>(
            BuildReportUri($"branches/{branchId:D}/reports/gameplay-time", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<CashOperationReportResultDto> GetCashOperationReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetAsync<CashOperationReportResultDto>(
            BuildReportUri($"branches/{branchId:D}/reports/cash-operations", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<OperatorActionReportResultDto> GetOperatorActionReportAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetAsync<OperatorActionReportResultDto>(
            BuildReportUri($"branches/{branchId:D}/reports/operator-actions", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<string> ExportShiftReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetStringAsync(
            BuildReportUri($"branches/{branchId:D}/reports/shifts/export.csv", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<string> ExportSalesReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetStringAsync(
            BuildReportUri($"branches/{branchId:D}/reports/sales/export.csv", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<string> ExportGameplayTimeReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetStringAsync(
            BuildReportUri($"branches/{branchId:D}/reports/gameplay-time/export.csv", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<string> ExportCashOperationReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetStringAsync(
            BuildReportUri($"branches/{branchId:D}/reports/cash-operations/export.csv", fromUtc, toUtc, limit),
            cancellationToken);
    }

    public Task<string> ExportOperatorActionReportCsvAsync(
        Guid branchId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit,
        CancellationToken cancellationToken)
    {
        return SendGetStringAsync(
            BuildReportUri($"branches/{branchId:D}/reports/operator-actions/export.csv", fromUtc, toUtc, limit),
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

    private async Task<TResponse> SendGetAsync<TResponse>(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        return result ?? throw new InvalidOperationException("Platform API returned an empty report response.");
    }

    private async Task<string> SendGetStringAsync(
        string requestUri,
        CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, requestUri, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    private static string BuildReportUri(
        string basePath,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? limit)
    {
        var query = new List<string>();
        if (fromUtc is not null)
        {
            query.Add($"fromUtc={Uri.EscapeDataString(fromUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (toUtc is not null)
        {
            query.Add($"toUtc={Uri.EscapeDataString(toUtc.Value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture))}");
        }

        if (limit is not null)
        {
            query.Add($"limit={limit.Value.ToString(CultureInfo.InvariantCulture)}");
        }

        return query.Count == 0 ? basePath : $"{basePath}?{string.Join("&", query)}";
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

        var request = new HttpRequestMessage(method, OrganizationApiRoute.Build(snapshot, requestUri));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
