using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.Shared.Contracts.Diagnostics;

namespace AFK4.OrganizationAdmin.App.Diagnostics;

public sealed class HttpOperatorDiagnosticsApiClient(
    HttpClient httpClient,
    IOrganizationAdminTokenStore tokenStore) : IOperatorDiagnosticsApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<BranchDiagnosticsDto> GetDiagnosticsAsync(
        Guid branchId,
        CancellationToken cancellationToken)
    {
        using var message = await CreateRequestAsync(
            HttpMethod.Get,
            $"branches/{branchId:D}/diagnostics",
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

        var result = await response.Content.ReadFromJsonAsync<BranchDiagnosticsDto>(
            JsonOptions,
            cancellationToken);

        return result ?? throw new InvalidOperationException("Platform API returned an empty diagnostics response.");
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
