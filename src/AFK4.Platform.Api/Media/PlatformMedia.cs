using AFK4.Shared.Contracts.Ads;
using AFK4.Shared.Contracts.Media;

namespace AFK4.Platform.Api.Media;

/// <summary>
/// Картинки платформы в общем хранилище (MinIO): обложки каталога игр и картинки рекламы. Клубных
/// записей у них нет — только объект и его публичный адрес.
/// </summary>
public static class PlatformMedia
{
    public static bool IsKnown(string purpose) =>
        purpose is PlatformMediaPurposeNames.CatalogCover or PlatformMediaPurposeNames.AdCreative;

    /// <summary>Проверяет, что это картинка, и кладёт её в `platform/{purpose}/`. Ошибка — кодом из <see cref="PlatformMediaErrorCodeNames"/>.</summary>
    public static async Task<(string? Url, string? Error)> PutAsync(
        IMediaStorage storage, MediaOptions options, string purpose, Stream content, long sizeBytes, CancellationToken ct)
    {
        if (!options.S3.IsConfigured) return (null, PlatformMediaErrorCodeNames.StorageNotConfigured);
        // Картинку рекламы сервер потом копирует себе с пределом AdLimits — больше его принимать незачем.
        var maxBytes = purpose == PlatformMediaPurposeNames.AdCreative ? Math.Min(options.MaxBytes, AdLimits.ImageMaxBytes) : options.MaxBytes;
        if (sizeBytes <= 0 || sizeBytes > maxBytes) return (null, PlatformMediaErrorCodeNames.TooLarge);

        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, ct);
        var bytes = buffer.ToArray();
        var contentType = MediaValidation.SniffImageContentType(bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
        if (contentType is null) return (null, PlatformMediaErrorCodeNames.NotAnImage);

        var objectKey = $"platform/{purpose}/{Guid.NewGuid():N}.{MediaValidation.ExtensionFor(contentType)}";
        using var upload = new MemoryStream(bytes);
        return (await storage.PutAsync(objectKey, contentType, upload, ct), null);
    }
}

/// <summary>Откуда берётся обложка игры из Steam. В тестах — подставная, без сети.</summary>
public interface ISteamCoverSource
{
    /// <summary>Картинка игры и её тип; null — у Steam такой нет или не ответил.</summary>
    Task<(byte[] Bytes, string ContentType)?> FetchAsync(string steamAppId, CancellationToken ct);
}

/// <summary>
/// Обложка из магазина Steam (владелец, 2026-09-26: «пусть подтягивается красивая картинка»).
/// Плитка игры на ПК — 16:9, ближе всего к ней капсула 616×353; нет её — шапка страницы.
/// </summary>
public sealed class SteamCdnCoverSource(IHttpClientFactory httpClients) : ISteamCoverSource
{
    public const string HttpClientName = "steam-covers";

    private static readonly string[] Candidates = ["capsule_616x353.jpg", "header.jpg"];

    public async Task<(byte[] Bytes, string ContentType)?> FetchAsync(string steamAppId, CancellationToken ct)
    {
        var http = httpClients.CreateClient(HttpClientName);
        foreach (var file in Candidates)
        {
            try
            {
                using var response = await http.GetAsync($"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId}/{file}", ct);
                if (!response.IsSuccessStatusCode) continue;
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                if (bytes.Length is 0 or > 2 * 1024 * 1024) continue;
                var contentType = MediaValidation.SniffImageContentType(bytes.AsSpan(0, Math.Min(bytes.Length, 16)));
                if (contentType is not null) return (bytes, contentType);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                // Steam не ответил — пробуем следующую картинку, потом сдаёмся: обложка не обязательна.
            }
        }

        return null;
    }
}

public static class SteamCovers
{
    /// <summary>
    /// Копия обложки Steam в нашем хранилище — адрес Steam может смениться, а ПК клубов держат его в
    /// кэше. Хранилище не настроено — отдаём сам адрес Steam: картинка важнее, чем место её хранения.
    /// </summary>
    public static async Task<string?> StoreAsync(
        ISteamCoverSource source, IMediaStorage storage, MediaOptions options, string steamAppId, CancellationToken ct)
    {
        var image = await source.FetchAsync(steamAppId, ct);
        if (image is not { } found) return null;
        if (!options.S3.IsConfigured) return $"https://cdn.akamai.steamstatic.com/steam/apps/{steamAppId}/capsule_616x353.jpg";

        var objectKey = $"platform/{PlatformMediaPurposeNames.CatalogCover}/steam-{steamAppId}-{Guid.NewGuid():N}.{MediaValidation.ExtensionFor(found.ContentType)}";
        using var content = new MemoryStream(found.Bytes);
        return await storage.PutAsync(objectKey, found.ContentType, content, ct);
    }
}
