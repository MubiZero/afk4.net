using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Operator.App.Auth;
using AFK4.Shared.Contracts.Audit;

namespace AFK4.Operator.App.Audit;

public sealed class HttpOperatorAuditApiClient(HttpClient httpClient, IOperatorTokenStore tokenStore) : IOperatorAuditApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<AuditSearchResultDto> SearchAsync(
        OperatorAuditSearchRequest request,
        CancellationToken cancellationToken)
    {
        using var message = await CreateRequestAsync(
            HttpMethod.Get,
            BuildPath(request),
            cancellationToken);

        using var response = await httpClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var result = await response.Content.ReadFromJsonAsync<AuditSearchResultDto>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Platform API returned an empty audit search response.");
    }

    private static string BuildPath(OperatorAuditSearchRequest request)
    {
        var query = new List<string>();
        AddQuery(query, "action", request.Action);
        AddQuery(query, "outcome", request.Outcome);
        AddQuery(query, "targetType", request.TargetType);
        AddQuery(query, "fromUtc", request.FromUtc?.ToString("O", CultureInfo.InvariantCulture));
        AddQuery(query, "toUtc", request.ToUtc?.ToString("O", CultureInfo.InvariantCulture));
        AddQuery(query, "limit", request.Limit?.ToString(CultureInfo.InvariantCulture));

        var path = $"branches/{request.BranchId:D}/audit";
        return query.Count == 0
            ? path
            : $"{path}?{string.Join("&", query)}";
    }

    private static void AddQuery(List<string> query, string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        query.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value.Trim())}");
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

        var request = new HttpRequestMessage(method, OrganizationApiRoute.Build(snapshot, path));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
