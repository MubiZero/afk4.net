using System.Text.Json;
using AFK4.Shared.Contracts.Branches;

namespace AFK4.Platform.Api.Branches;

/// <summary>Сериализация галереи зала. Хранится одной JSON-колонкой branches.PhotosJson;
/// читается всегда списком, даже если в колонке мусор — витрина не должна падать из-за
/// того, что кто-то однажды записал туда не то.</summary>
public static class BranchPhotos
{
    /// Больше десятка фото зала никто не пролистает, а каждое из них — трафик игрока на
    /// экране выбора клуба.
    public const int MaxPhotos = 10;

    public const int MaxUrlLength = 600;

    public static string Serialize(IReadOnlyList<BranchPhotoDto> photos) => JsonSerializer.Serialize(photos);

    public static IReadOnlyList<BranchPhotoDto> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<List<BranchPhotoDto>>(json);
            return parsed is null
                ? []
                : parsed.Where(photo => !string.IsNullOrWhiteSpace(photo.Url)).ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    public static string? Validate(IReadOnlyList<BranchPhotoDto>? photos)
    {
        if (photos is null)
        {
            return null;
        }

        if (photos.Count > MaxPhotos)
        {
            return $"Photos must contain {MaxPhotos} items or fewer.";
        }

        foreach (var photo in photos)
        {
            if (string.IsNullOrWhiteSpace(photo.Url))
            {
                return "Photo URL is required.";
            }

            if (photo.Url.Length > MaxUrlLength)
            {
                return $"Photo URL must contain {MaxUrlLength} characters or fewer.";
            }
        }

        return null;
    }
}
