using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace AFK4.Platform.Api.Payments.DcGate;

public sealed class DcGateClient : IDcGateClient
{
    private readonly HttpClient httpClient;
    private readonly string apiKey;

    public DcGateClient(HttpClient httpClient, string apiKey)
    {
        this.httpClient = httpClient;
        this.apiKey = apiKey;
    }

    public async Task<DcGatePaymentResult> CreatePaymentAsync(
        long amountMinorUnits,
        string currencyCode,
        string externalOrderId,
        object metadata,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new
            {
                amount = ToMajorUnitString(amountMinorUnits),
                externalOrderId,
                metadata
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<DcGatePaymentResult>(cancellationToken);
        return result ?? throw new HttpRequestException("dcgate returned an empty payment body.");
    }

    // Money stays long minor units inside AFK4; dcgate's wire format is a
    // major-unit decimal string. This boundary is the ONLY place we convert.
    private static string ToMajorUnitString(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
