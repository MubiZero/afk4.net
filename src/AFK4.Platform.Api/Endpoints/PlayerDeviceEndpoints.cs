using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Notifications;
using AFK4.Shared.Contracts.Notifications;

namespace AFK4.Platform.Api.Endpoints;

/// Телефон игрока как адрес доставки. Приложение сообщает свой токен при входе и снимает его при
/// выходе — иначе уведомления о чужой сессии придут тому, кто просто одолжил телефон.
internal static class PlayerDeviceEndpoints
{
    private const int MaxTokenLength = 512;

    private static readonly string[] KnownPlatforms = ["android", "ios"];

    public static void MapPlayerDeviceEndpoints(this WebApplication app)
    {
        app.MapPost("/api/me/devices", async (
            RegisterPlayerDeviceRequest request,
            IPlayerContextAccessor playerContextAccessor,
            IPlayerDeviceStore devices,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            var token = request.PushToken?.Trim();
            if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
            {
                return Results.BadRequest(new { error = "push_token_invalid" });
            }

            var platform = request.Platform?.Trim().ToLowerInvariant();
            if (platform is null || !KnownPlatforms.Contains(platform))
            {
                return Results.BadRequest(new { error = "platform_invalid" });
            }

            await devices.RegisterAsync(player.PlayerAccountId, token, platform, Trimmed(request.Locale), cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("player-me");

        // Выход из аккаунта. Приложение зовёт это до того, как выбросить сессию: после выхода
        // звать уже нечем, а токен останется висеть на прежнем игроке.
        app.MapDelete("/api/me/devices/{pushToken}", async (
            string pushToken,
            IPlayerContextAccessor playerContextAccessor,
            IPlayerDeviceStore devices,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null)
            {
                return Results.Unauthorized();
            }

            // Своё устройство снимается молча и повторно: удалять уже удалённое — не ошибка.
            await devices.RemoveAsync(player.PlayerAccountId, pushToken, cancellationToken);
            return Results.NoContent();
        }).RequireRateLimiting("player-me").WorksWhenNetworkBanned();
    }

    private static string? Trimmed(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
