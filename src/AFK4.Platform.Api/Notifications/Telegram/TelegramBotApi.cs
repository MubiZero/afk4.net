using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Notifications.Telegram;

/// <summary>Отказ Bot API. <see cref="IsPermanent"/> — повтор не поможет: бот заблокирован, чата нет.</summary>
public sealed class TelegramApiException(string message, bool isPermanent) : Exception(message)
{
    public bool IsPermanent { get; } = isPermanent;
}

public sealed record TelegramIncomingMessage(long UpdateId, long ChatId, string Text, string? LanguageCode);

/// <summary>Три метода Bot API, которые нужны платформе. Интерфейс — ради теста без сети.</summary>
public interface ITelegramBotApi
{
    Task<string> GetBotUserNameAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<TelegramIncomingMessage>> GetUpdatesAsync(long offset, CancellationToken cancellationToken);

    Task SendHtmlAsync(long chatId, string html, CancellationToken cancellationToken);
}

public static class TelegramClientRegistration
{
    public const string HttpClientName = "telegram";
}

/// <summary>
/// Bot API поверх HttpClient. Токен стоит в пути запроса (<c>/bot&lt;token&gt;/sendMessage</c>), поэтому
/// у этого клиента журнал запросов фабрики выключен (см. Program.cs): иначе токен уходил бы в логи.
/// </summary>
public sealed class TelegramBotApi(IHttpClientFactory httpClientFactory, IOptions<TelegramOptions> options) : ITelegramBotApi
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private string? botUserName;

    public async Task<string> GetBotUserNameAsync(CancellationToken cancellationToken)
    {
        if (botUserName is not null)
        {
            return botUserName;
        }

        var me = await CallAsync<BotUser>("getMe", new { }, Client(), cancellationToken);
        return botUserName = me.Username ?? throw new TelegramApiException("Telegram getMe returned no username.", isPermanent: true);
    }

    public async Task<IReadOnlyList<TelegramIncomingMessage>> GetUpdatesAsync(long offset, CancellationToken cancellationToken)
    {
        // Ждём дольше, чем сервер держит запрос: иначе каждый тихий long poll кончался бы таймаутом клиента.
        var client = Client();
        client.Timeout = TimeSpan.FromSeconds(options.Value.PollTimeoutSeconds + options.Value.TimeoutSeconds);
        var updates = await CallAsync<Update[]>(
            "getUpdates",
            new { offset, timeout = options.Value.PollTimeoutSeconds, allowed_updates = new[] { "message" } },
            client,
            cancellationToken);

        return updates
            .Select(update => update.Message is { Chat: not null } message
                ? new TelegramIncomingMessage(update.UpdateId, message.Chat.Id, message.Text ?? string.Empty, message.From?.LanguageCode)
                : new TelegramIncomingMessage(update.UpdateId, 0, string.Empty, null))
            .ToList();
    }

    public Task SendHtmlAsync(long chatId, string html, CancellationToken cancellationToken) =>
        CallAsync<JsonElement>(
            "sendMessage",
            new { chat_id = chatId, text = html, parse_mode = "HTML", link_preview_options = new { is_disabled = true } },
            Client(),
            cancellationToken);

    private HttpClient Client()
    {
        var client = httpClientFactory.CreateClient(TelegramClientRegistration.HttpClientName);
        client.BaseAddress = new Uri($"{options.Value.BaseUrl.TrimEnd('/')}/bot{options.Value.BotToken}/");
        return client;
    }

    private static async Task<T> CallAsync<T>(string method, object body, HttpClient client, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(method, body, Json, cancellationToken);
        }
        catch (HttpRequestException exception)
        {
            // Сообщение исключения может содержать адрес запроса, а в нём токен — наружу только метод.
            throw new TelegramApiException($"Telegram {method} failed: {exception.HttpRequestError}.", isPermanent: false);
        }

        using (response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<Envelope<T>>(Json, cancellationToken);
            if (response.IsSuccessStatusCode && envelope is { Ok: true, Result: not null })
            {
                return envelope.Result;
            }

            var description = envelope?.Description ?? response.ReasonPhrase ?? "no description";
            // 400 «chat not found» и 403 «bot was blocked by the user» не лечатся повтором; 429 и 5xx — лечатся.
            var permanent = response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized or HttpStatusCode.NotFound;
            throw new TelegramApiException($"Telegram {method}: {(int)response.StatusCode} {description}", permanent);
        }
    }

    private sealed record Envelope<T>(bool Ok, T? Result, string? Description);

    private sealed record BotUser(string? Username);

    private sealed record Update(long UpdateId, Message? Message);

    private sealed record Message(Chat? Chat, string? Text, Sender? From);

    private sealed record Chat(long Id);

    private sealed record Sender(string? LanguageCode);
}
