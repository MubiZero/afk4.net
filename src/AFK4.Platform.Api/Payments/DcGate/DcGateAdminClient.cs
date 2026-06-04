using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFK4.Platform.Api.Payments.DcGate;

// Thrown when dcgate returns a non-success status; carries the dcgate message verbatim
// so the owner endpoint can relay it as a 4xx (Subsystem B error-handling contract).
public sealed class DcGateAdminException(HttpStatusCode statusCode, string message) : Exception(message)
{
    public HttpStatusCode StatusCode { get; } = statusCode;
}

public sealed class DcGateAdminClient : IDcGateAdminClient
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly HttpClient httpClient;
    private readonly string adminSecret;

    public DcGateAdminClient(HttpClient httpClient, string adminSecret)
    {
        this.httpClient = httpClient;
        this.adminSecret = adminSecret;
    }

    public async Task<DcGateAdminProjectResult> CreateProjectAsync(
        DcGateCreateProjectRequest request, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post, "/api/admin/projects", new
        {
            name = request.Name,
            cardNumber = request.CardNumber,
            webhookUrl = request.WebhookUrl,
            paymentExpiresInMinutes = request.PaymentExpiresInMinutes,
            externalId = request.ExternalId
        });

        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateAdminProjectResult(
            Id: root.GetProperty("id").GetString() ?? throw Empty("id"),
            Status: root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "",
            CardLast4: root.TryGetProperty("cardLast4", out var cl) ? cl.GetString() ?? "" : "",
            ApiKey: root.TryGetProperty("apiKey", out var ak) ? ak.GetString() : null,
            WebhookSecret: root.TryGetProperty("webhookSecret", out var ws) ? ws.GetString() : null,
            IdempotentReplay: root.TryGetProperty("idempotentReplay", out var ir) && ir.GetBoolean());
    }

    public async Task<DcGateTelegramStartResult> StartTelegramAsync(
        string dcgateProjectId, string phone, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/start", new { phone });
        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateTelegramStartResult(
            root.GetProperty("loginAttemptId").GetString() ?? throw Empty("loginAttemptId"),
            root.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateTelegramVerifyResult> VerifyTelegramCodeAsync(
        string dcgateProjectId, string loginAttemptId, string code, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/verify-code",
            new { loginAttemptId, code });
        using var doc = await SendAsync(http, cancellationToken);
        return new DcGateTelegramVerifyResult(
            doc.RootElement.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateTelegramVerifyResult> VerifyTelegramPasswordAsync(
        string dcgateProjectId, string loginAttemptId, string password, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Post,
            $"/api/admin/projects/{dcgateProjectId}/telegram-session/verify-password",
            new { loginAttemptId, password });
        using var doc = await SendAsync(http, cancellationToken);
        return new DcGateTelegramVerifyResult(
            doc.RootElement.GetProperty("state").GetString() ?? throw Empty("state"));
    }

    public async Task<DcGateProjectStatusResult> GetStatusAsync(
        string dcgateProjectId, CancellationToken cancellationToken)
    {
        using var http = BuildRequest(HttpMethod.Get,
            $"/api/admin/projects/{dcgateProjectId}/status", content: null);
        using var doc = await SendAsync(http, cancellationToken);
        var root = doc.RootElement;
        return new DcGateProjectStatusResult(
            SessionHealth: root.TryGetProperty("sessionHealth", out var sh) ? sh.GetString() ?? "offline" : "offline",
            LastConnectedAt: ReadDate(root, "lastConnectedAt"),
            LastMessageAt: ReadDate(root, "lastMessageAt"),
            TelegramMessagesCount: root.TryGetProperty("telegramMessagesCount", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : 0);
    }

    private HttpRequestMessage BuildRequest(HttpMethod method, string path, object? content)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.TryAddWithoutValidation("x-admin-secret", adminSecret);
        if (content is not null)
        {
            request.Content = JsonContent.Create(content);
        }
        return request;
    }

    private async Task<JsonDocument> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new DcGateAdminException(response.StatusCode, ExtractMessage(payload, response.StatusCode));
        }
        return JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
    }

    private static string ExtractMessage(string payload, HttpStatusCode status)
    {
        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                if (doc.RootElement.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String)
                {
                    return m.GetString()!;
                }
            }
            catch (JsonException) { /* fall through to status text */ }
        }
        return $"dcgate admin call failed ({(int)status}).";
    }

    private static DateTimeOffset? ReadDate(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetDateTimeOffset()
            : null;

    private static InvalidOperationException Empty(string field) =>
        new($"dcgate admin response missing '{field}'.");
}

public static class DcGateAdminClientRegistration
{
    public const string HttpClientName = "dcgate-admin";
}
