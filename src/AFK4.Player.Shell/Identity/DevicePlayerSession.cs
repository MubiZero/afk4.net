using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Identity;

/// <summary>
/// Вход игрока на этом ПК глазами хоста (спека оболочки, §5.3). Токены приходят от агента — хост за
/// ними к серверу не ходит, ключа ПК у него нет. Держатся только в памяти: страница их не видит,
/// хост подставляет заголовок сам. Сервер гасит их и без хоста — хост лишь не мешает.
/// </summary>
public sealed class DevicePlayerSession(HttpClient http, Func<string?> apiBaseUrl, TimeProvider timeProvider)
{
    /// <summary>Обновлять заранее: запрос страницы не должен упереться в истёкший доступ.</summary>
    public static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly Lock gate = new();
    private readonly SemaphoreSlim refreshing = new(1, 1);
    private PlatformPersonSessionResponse? session;

    public ShellAuthStateDto Current
    {
        get
        {
            lock (gate)
            {
                return session is null
                    ? new ShellAuthStateDto(SignedIn: false)
                    : new ShellAuthStateDto(true, session.DisplayName, session.PlayerAccountId);
            }
        }
    }

    public string? AccessToken
    {
        get
        {
            lock (gate)
            {
                return session?.AccessToken;
            }
        }
    }

    /// <summary>Агент прислал вход — ПИН-кодом или по QR.</summary>
    public ShellAuthStateDto Accept(PlatformPersonSessionResponse signedIn)
    {
        lock (gate)
        {
            session = signedIn;
        }

        return Current;
    }

    /// <summary>
    /// Забыть вход без похода на сервер: клуб уже погасил токены (команда sign-out) или сервер их
    /// не принял.
    /// </summary>
    public bool Forget()
    {
        lock (gate)
        {
            var wasSignedIn = session is not null;
            session = null;
            return wasSignedIn;
        }
    }

    /// <summary>Игрок вышел сам: погасить токены на сервере и забыть. Сбой сети выход не отменяет.</summary>
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        PlatformPersonSessionResponse? leaving;
        lock (gate)
        {
            leaving = session;
            session = null;
        }

        var endpoint = Endpoint("/api/public/player/sign-out");
        if (leaving is null || endpoint is null)
        {
            return;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = JsonContent.Create(new PlayerSignOutRequest(leaving.RefreshToken), options: Json)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", leaving.AccessToken);
            using var response = await http.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                          && !cancellationToken.IsCancellationRequested)
        {
            // Токены привязаны к ПК: без сессии сервер погасит их сам через несколько минут.
            PlayerShellStartupLog.Write("Sign-out did not reach the platform; the server will expire the tokens.", exception);
        }
    }

    /// <summary>
    /// Обновить доступ, если он вот-вот истечёт. true — вход кончился (сервер отказал в обновлении):
    /// странице надо сказать об этом, иначе она рисовала бы вошедшего, чьи запросы сыплют 401.
    /// </summary>
    public async Task<bool> EnsureFreshAsync(CancellationToken cancellationToken)
    {
        if (!NeedsRefresh(out _))
        {
            return false;
        }

        await refreshing.WaitAsync(cancellationToken);
        try
        {
            if (!NeedsRefresh(out var current) || Endpoint("/api/public/player/refresh") is not { } endpoint)
            {
                return false;
            }

            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync(endpoint, new PlayerRefreshRequest(current!.RefreshToken), Json, cancellationToken);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException
                                              && !cancellationToken.IsCancellationRequested)
            {
                // Мигнула сеть — вход не трогаем, попробуем на следующем круге.
                return false;
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                {
                    return ForgetIfStill(current);
                }

                if (!response.IsSuccessStatusCode)
                {
                    return false;
                }

                var refreshed = await response.Content.ReadFromJsonAsync<PlatformPersonSessionResponse>(Json, cancellationToken);
                lock (gate)
                {
                    // Пока шло обновление, игрок мог выйти или войти другой — чужой ответ не записываем.
                    if (refreshed is not null && ReferenceEquals(session, current))
                    {
                        session = refreshed;
                    }
                }

                return false;
            }
        }
        finally
        {
            refreshing.Release();
        }
    }

    private bool NeedsRefresh(out PlatformPersonSessionResponse? current)
    {
        lock (gate)
        {
            current = session;
            return current is not null && timeProvider.GetUtcNow() >= current.AccessTokenExpiresAtUtc - RefreshSkew;
        }
    }

    private bool ForgetIfStill(PlatformPersonSessionResponse expired)
    {
        lock (gate)
        {
            if (!ReferenceEquals(session, expired))
            {
                return false;
            }

            session = null;
            return true;
        }
    }

    private Uri? Endpoint(string path) =>
        Uri.TryCreate(apiBaseUrl(), UriKind.Absolute, out var baseUri) ? new Uri(baseUri, path) : null;
}
