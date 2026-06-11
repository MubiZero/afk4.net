using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Identity;

/// Holds player tokens in memory ONLY (never handed to JS). The WebView injects
/// CurrentAccessToken on API-origin requests; this client refreshes proactively.
public sealed class PlayerApiAuthClient(HttpClient http) : IPlayerApiAuthClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);
    private readonly SemaphoreSlim gate = new(1, 1);

    private string? accessToken;
    private string? refreshToken;
    private DateTimeOffset accessExpiresAtUtc;
    private AuthSnapshot current;

    public AuthSnapshot Current => current;
    public string? CurrentAccessToken => accessToken;

    public async Task<AuthSnapshot> SignInAsync(Guid organizationId, string phoneNumber, string password, CancellationToken ct)
    {
        var response = await http.PostAsJsonAsync(
            "/api/public/player/sign-in",
            new PlayerSignInRequest(organizationId, phoneNumber, password),
            Json,
            ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized || !response.IsSuccessStatusCode)
        {
            return current = new AuthSnapshot(false, null, false);
        }

        var body = await response.Content.ReadFromJsonAsync<PlayerSignInResponse>(Json, ct);
        return body is null ? (current = new AuthSnapshot(false, null, false)) : Store(body);
    }

    public async Task EnsureFreshTokenAsync(CancellationToken ct)
    {
        if (refreshToken is null || DateTimeOffset.UtcNow < accessExpiresAtUtc - RefreshSkew)
        {
            return;
        }

        await gate.WaitAsync(ct);
        try
        {
            if (refreshToken is null || DateTimeOffset.UtcNow < accessExpiresAtUtc - RefreshSkew)
            {
                return;
            }

            HttpResponseMessage response;
            try
            {
                response = await http.PostAsJsonAsync(
                    "/api/public/player/refresh", new PlayerRefreshRequest(refreshToken), Json, ct);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // Transient transport failure (DNS/refused/TLS/timeout): keep the tokens and retry
                // next tick. Signing out here would log the player out on a brief network blip.
                return;
            }

            // Only a definitive auth rejection invalidates the session. A 5xx is a server-side
            // blip — keep the tokens so a momentary platform hiccup doesn't drop the player.
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                SignOut();
                return;
            }

            if (!response.IsSuccessStatusCode)
            {
                return;
            }

            var body = await response.Content.ReadFromJsonAsync<PlayerSignInResponse>(Json, ct);
            if (body is not null)
            {
                Store(body);
            }
        }
        finally
        {
            gate.Release();
        }
    }

    public void SignOut()
    {
        accessToken = null;
        refreshToken = null;
        accessExpiresAtUtc = default;
        current = new AuthSnapshot(false, null, false);
    }

    private AuthSnapshot Store(PlayerSignInResponse body)
    {
        accessToken = body.AccessToken;
        refreshToken = body.RefreshToken;
        accessExpiresAtUtc = body.AccessTokenExpiresAtUtc;
        return current = new AuthSnapshot(true, body.DisplayName, body.PhoneVerified);
    }
}
