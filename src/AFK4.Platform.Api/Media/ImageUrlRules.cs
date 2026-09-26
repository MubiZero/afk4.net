namespace AFK4.Platform.Api.Media;

/// <summary>
/// Адрес картинки, которую запись хранит текстом (новость, фото товара): загрузка в медиа-хранилище
/// отдаёт такой адрес, но клуб может вписать и свой. Годится только абсолютный http или https —
/// иначе экран ПК и приложение получили бы то, что не открыть.
/// </summary>
public static class ImageUrlRules
{
    public const int MaxLength = 2048;

    public static string? Validate(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl)) return null;
        if (imageUrl.Length > MaxLength) return $"Image URL must be at most {MaxLength} characters.";
        return Uri.TryCreate(imageUrl.Trim(), UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? null
            : "Image URL must be an absolute http or https address.";
    }

    public static string? Normalize(string? imageUrl) =>
        string.IsNullOrWhiteSpace(imageUrl) ? null : imageUrl.Trim();
}
