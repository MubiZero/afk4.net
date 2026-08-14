using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Отправка через payom.tj.
///
/// Шлюз принимает только заранее одобренный шаблон: <c>templateId</c> плюс значения
/// плейсхолдеров. Свободный текст отклоняется с 422 — поэтому текста в запросе нет вовсе, он
/// живёт на стороне шлюза и меняется только через новую модерацию.
/// </summary>
public sealed class PayomSmsTransport : ISmsTransport
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly HttpClient httpClient;
    private readonly string apiToken;
    private readonly string senderName;

    public PayomSmsTransport(HttpClient httpClient, string apiToken, string senderName)
    {
        this.httpClient = httpClient;
        this.apiToken = apiToken;
        this.senderName = senderName;
    }

    public async Task SendAsync(SmsMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(apiToken))
        {
            // Ненастроенный шлюз — не сетевой сбой: повторять нечего, чинится конфигурацией.
            throw new SmsTransportException(isPermanent: true, "Payom API token is not configured.");
        }

        var variables = new JsonObject();
        foreach (var (key, value) in message.Variables)
        {
            variables[key] = value;
        }

        var payload = new JsonObject
        {
            ["telephone"] = message.ToPhoneNumber,
            ["senderName"] = senderName,
            ["type"] = "SMS",
            ["templateMessage"] = new JsonObject
            {
                ["templateId"] = message.TemplateId,
                ["variables"] = variables,
            },
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "api/message");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiToken}");
        request.Content = new StringContent(
            payload.ToJsonString(JsonOptions), System.Text.Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(request, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            throw new SmsTransportException(isPermanent: false, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new SmsTransportException(isPermanent: false, exception.Message);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var status = (int)response.StatusCode;
                // 5xx и 429 — «попробуйте позже», остальное про запрос: отклонённый шаблон,
                // неверный формат плейсхолдера, чужое имя отправителя. Повтор их не вылечит.
                var transient = status >= 500 || status == 429;
                throw new SmsTransportException(isPermanent: !transient, $"{status} {Describe(body)}");
            }

            // Успех без идентификатора — это не успех: шлюз отвечает {id, deliveryStatus},
            // и без id мы не отличим принятое сообщение от проглоченного.
            if (string.IsNullOrWhiteSpace(ReadString(body, "id")))
            {
                throw new SmsTransportException(isPermanent: false, $"Payom returned no message id: {Describe(body)}");
            }
        }
    }

    /// <summary>Полезное у payom лежит в <c>detail</c> (RFC 7807); дальше — по убыванию удачи.</summary>
    private static string Describe(string body)
    {
        foreach (var key in (string[])["detail", "message", "error"])
        {
            var value = ReadString(body, key);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return body.Length <= 300 ? body : body[..300];
    }

    private static string? ReadString(string body, string property)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonNode.Parse(body) is JsonObject json && json[property] is JsonValue value
                ? value.ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
