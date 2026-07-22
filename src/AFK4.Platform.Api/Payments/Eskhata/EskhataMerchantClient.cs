using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFK4.Platform.Api.Payments.Eskhata;

public sealed class EskhataMerchantClient : IEskhataMerchantClient
{
    private const int OrderTypeDynamicPos = 3;
    private readonly HttpClient httpClient;
    private readonly string companyId;
    private readonly string hashKey;

    public EskhataMerchantClient(HttpClient httpClient, string companyId, string hashKey)
    {
        this.httpClient = httpClient;
        this.companyId = companyId;
        this.hashKey = hashKey;
    }

    public async Task<EskhataCreateOrderResult> CreateOrderAsync(
        string invoiceId, long amountMinor, string currencyCode, string description,
        int merchantId, CancellationToken cancellationToken)
    {
        var amount = EskhataSigner.FormatAmount(amountMinor);
        // ⚠️ ПОРЯДОК ХЕША ДЛЯ orderTypeId=3 — ЭМПИРИЧЕСКАЯ НЕИЗВЕСТНОСТЬ (см. Task 8).
        // Базовая гипотеза: как в типе 1/2, но posId выпадает, merchantId встаёт после description.
        var hash = EskhataSigner.BuildHash(
            new[] { invoiceId, amount, currencyCode, description, merchantId.ToString(), OrderTypeDynamicPos.ToString() },
            hashKey);

        var body = new
        {
            hash,
            invoiceId,
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            currency = currencyCode,
            description,
            merchantId,
            orderTypeId = OrderTypeDynamicPos
        };

        var data = await PostAsync("/merchant/api/v1/orders/create", body, cancellationToken);
        var orderId = GetString(data, "orderId") ?? throw new HttpRequestException("Eskhata: empty orderId");
        return new EskhataCreateOrderResult(
            orderId,
            GetString(data, "orderStatus") ?? "NEW",
            GetString(data, "qr"),
            GetString(data, "invoiceUrl") ?? GetString(data, "InvoiceUrl"),
            GetInt(data, "posId"));
    }

    public async Task<string?> GetOrderStatusAsync(
        string invoiceId, string orderId, long amountMinor, string currencyCode, int posId,
        CancellationToken cancellationToken)
    {
        var amount = EskhataSigner.FormatAmount(amountMinor);
        var hash = EskhataSigner.BuildHash(
            new[] { invoiceId, orderId, amount, currencyCode, posId.ToString() }, hashKey);
        var body = new
        {
            hash, invoiceId, orderId,
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            currency = currencyCode, posId
        };
        try
        {
            var data = await PostAsync("/merchant/api/v1/orders/status", body, cancellationToken);
            return GetString(data, "orderStatus");
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException)
        {
            return null;
        }
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CompanyId", EskhataSigner.CompanyIdHeader(companyId));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        if (!root.TryGetProperty("status", out var ok) || !ok.GetBoolean())
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Eskhata request failed";
            throw new HttpRequestException($"Eskhata: {msg}");
        }
        return root.GetProperty("data").Clone();
    }

    private static string? GetString(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
}
