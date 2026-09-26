using System.Security.Cryptography;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Ads;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Ads;

/// <summary>
/// Копия картинки одобренного креатива (спека рекламы, §8.1): показывать надо ровно то, что одобрил
/// модератор, — не то, что лежит по ссылке рекламодателя сегодня. Копия же отвечает проверяющему,
/// что было на экранах клубов (ст. 25): клуб — тоже распространитель.
/// </summary>
public static class AdCreativeImages
{
    public const string HttpClientName = "ad-images";

    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png", "image/jpeg", "image/webp"
    };

    public static string Extension(string contentType) => contentType switch
    {
        "image/png" => ".png",
        "image/webp" => ".webp",
        _ => ".jpg"
    };

    /// <summary>Скачивает картинку по адресу креатива и кладёт копию. Нет картинки — false.</summary>
    public static async Task<bool> StoreAsync(
        PlatformDbContext db, HttpClient http, AdCreativeEntity creative, DateTimeOffset now, CancellationToken ct)
    {
        if (!Uri.TryCreate(creative.ImageUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;

        byte[] bytes;
        string contentType;
        try
        {
            using var response = await http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, ct);
            contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!response.IsSuccessStatusCode || !AllowedTypes.Contains(contentType)) return false;
            if (response.Content.Headers.ContentLength > AdLimits.ImageMaxBytes) return false;

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var buffer = new MemoryStream();
            var chunk = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(chunk, ct)) > 0)
            {
                if (buffer.Length + read > AdLimits.ImageMaxBytes) return false;
                buffer.Write(chunk, 0, read);
            }

            bytes = buffer.ToArray();
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            return false;
        }

        var sha = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var image = await db.AdCreativeImages.SingleOrDefaultAsync(candidate => candidate.CreativeId == creative.CreativeId, ct);
        if (image is null)
        {
            image = new AdCreativeImageEntity { CreativeId = creative.CreativeId };
            db.AdCreativeImages.Add(image);
        }

        image.Bytes = bytes;
        image.ContentType = contentType.ToLowerInvariant();
        image.Sha256 = sha;
        image.SourceUrl = uri.AbsoluteUri;
        image.StoredAtUtc = now;
        return true;
    }
}
