using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Отправка через Firebase Cloud Messaging (HTTP v1).
///
/// FCM не принимает служебный ключ напрямую: сначала им подписывается JWT, тот меняется на
/// токен доступа на час, и уже с ним уходят сообщения. Токен кэшируется — иначе на каждый пуш
/// приходилось бы по два похода в сеть вместо одного.
/// </summary>
public sealed class FcmPushTransport(HttpClient http, PushOptions options, TimeProvider timeProvider)
    : IPushTransport, IDisposable
{
    /// <summary>Насколько раньше срока обновляем токен: у часового токена запас в минуту — это тесно.</summary>
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(5);

    private readonly SemaphoreSlim tokenLock = new(1, 1);
    private string? accessToken;
    private DateTimeOffset accessTokenExpiresUtc;

    public async Task SendAsync(PushMessage message, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (!options.IsConfigured)
        {
            // Ключей нет — это не поломка сети, а ненастроенный канал: повторять бессмысленно.
            throw new PushTransportException(isPermanent: true, "Push is not configured.");
        }

        var token = await GetAccessTokenAsync(cancellationToken);

        var payload = new JsonObject
        {
            ["message"] = new JsonObject
            {
                ["token"] = message.DeviceToken,
                ["notification"] = new JsonObject
                {
                    ["title"] = message.Title,
                    ["body"] = message.Body,
                },
            },
        };

        if (message.Data is { Count: > 0 })
        {
            var data = new JsonObject();
            foreach (var (key, value) in message.Data)
            {
                data[key] = value;
            }

            payload["message"]!["data"] = data;
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"https://fcm.googleapis.com/v1/projects/{options.ProjectId}/messages:send")
        {
            Content = new StringContent(payload.ToJsonString(), Encoding.UTF8, "application/json"),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PushTransportException(isPermanent: false, exception.Message);
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            // 404 и 400 с UNREGISTERED — приложение на этом телефоне больше не живёт.
            if (response.StatusCode == HttpStatusCode.NotFound || body.Contains("UNREGISTERED", StringComparison.Ordinal))
            {
                throw new PushTransportException(isPermanent: true, Trim(body), isUnregistered: true);
            }

            // 401/403 — отвергнутый ключ, 400 — испорченный запрос: повтор ничего не изменит.
            var permanent = response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.Unauthorized
                or HttpStatusCode.Forbidden;

            // Токен мог протухнуть раньше срока — заставим следующий вызов взять новый.
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                accessTokenExpiresUtc = DateTimeOffset.MinValue;
            }

            throw new PushTransportException(permanent, $"FCM {(int)response.StatusCode}: {Trim(body)}");
        }
    }

    public void Dispose() => tokenLock.Dispose();

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        if (accessToken is not null && now < accessTokenExpiresUtc - RenewBefore)
        {
            return accessToken;
        }

        await tokenLock.WaitAsync(cancellationToken);
        try
        {
            now = timeProvider.GetUtcNow();
            if (accessToken is not null && now < accessTokenExpiresUtc - RenewBefore)
            {
                return accessToken;
            }

            var assertion = BuildAssertion(now);
            using var response = await http.PostAsync(
                options.TokenEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion,
                }),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = Trim(await response.Content.ReadAsStringAsync(cancellationToken));
                // Отказ выдать токен — это про ключ, а не про сеть: чинится настройкой, не повтором.
                throw new PushTransportException(isPermanent: true, $"Google OAuth {(int)response.StatusCode}: {error}");
            }

            var token = await response.Content.ReadFromJsonAsync<GoogleAccessToken>(cancellationToken);
            if (token is null || string.IsNullOrWhiteSpace(token.AccessToken))
            {
                throw new PushTransportException(isPermanent: false, "Google OAuth returned no access token.");
            }

            accessToken = token.AccessToken;
            accessTokenExpiresUtc = now.AddSeconds(token.ExpiresIn <= 0 ? 3600 : token.ExpiresIn);
            return accessToken;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            throw new PushTransportException(isPermanent: false, exception.Message);
        }
        finally
        {
            tokenLock.Release();
        }
    }

    /// <summary>Подписанный служебным ключом JWT, который Google меняет на токен доступа.</summary>
    private string BuildAssertion(DateTimeOffset now)
    {
        var issuedAt = now.ToUnixTimeSeconds();
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = options.ClientEmail,
            scope = "https://www.googleapis.com/auth/firebase.messaging",
            aud = options.TokenEndpoint,
            iat = issuedAt,
            exp = issuedAt + 3600,
        }));

        var unsigned = $"{header}.{claims}";

        using var rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(options.PrivateKey);
        }
        catch (ArgumentException exception)
        {
            throw new PushTransportException(isPermanent: true, $"Push private key is not valid PEM: {exception.Message}");
        }

        var signature = rsa.SignData(
            Encoding.UTF8.GetBytes(unsigned), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{unsigned}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>Ответ провайдера идёт в LastError строки очереди — там ему хватит пары строк.</summary>
    private static string Trim(string body) => body.Length <= 500 ? body : body[..500];

    private sealed record GoogleAccessToken(
        [property: System.Text.Json.Serialization.JsonPropertyName("access_token")] string AccessToken,
        [property: System.Text.Json.Serialization.JsonPropertyName("expires_in")] int ExpiresIn);
}
