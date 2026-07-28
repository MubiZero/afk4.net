using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.OrganizationAdmin.App.Auth;
using AFK4.Shared.Contracts.FloorMap;

namespace AFK4.OrganizationAdmin.App.FloorMap;

public sealed class HttpOperatorFloorMapApiClient(HttpClient httpClient, IOrganizationAdminTokenStore tokenStore) : IOperatorFloorMapApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<FloorMapDto> GetFloorMapAsync(Guid branchId, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(branchId, cancellationToken);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"Platform API returned {(int)response.StatusCode} {response.ReasonPhrase}: {errorBody}",
                inner: null,
                response.StatusCode);
        }

        var floorMap = await response.Content.ReadFromJsonAsync<FloorMapDto>(JsonOptions, cancellationToken);
        return floorMap ?? throw new InvalidOperationException("Platform API returned an empty floor map response.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(Guid branchId, CancellationToken cancellationToken)
    {
        var snapshot = await tokenStore.LoadAsync(cancellationToken);
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.AccessToken))
        {
            throw new InvalidOperationException("Operator access token is missing.");
        }

        var request = new HttpRequestMessage(
            HttpMethod.Get,
            OrganizationApiRoute.Build(snapshot, $"branches/{branchId:D}/floor-map"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", snapshot.AccessToken);
        return request;
    }
}
