using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace AFK4.Platform.Api.Notifications;

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
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/message");
        request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiToken}");
        request.Content = JsonContent.Create(new
        {
            telephone = message.ToPhoneNumber,
            text = message.Text,
            senderName,
            type = "SMS",
        }, options: JsonOptions);

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
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var status = (int)response.StatusCode;
            var transient = status >= 500 || status == 429;
            throw new SmsTransportException(isPermanent: !transient, $"{status} {body}");
        }
    }
}
