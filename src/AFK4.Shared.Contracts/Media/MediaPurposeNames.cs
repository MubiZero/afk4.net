namespace AFK4.Shared.Contracts.Media;

public static class MediaPurposeNames
{
    public const string BranchLogo = "branch-logo";

    /// Логотип клуба целиком: его показывают приложение игрока, витрина и экран игрового ПК.
    /// Отдельно от логотипа зала — у сети он один, а залов много.
    public const string OrganizationLogo = "organization-logo";

    /// Фото зала для витрины клуба в приложении игрока.
    public const string BranchCover = "branch-cover";

    /// Остальные фото зала: их несколько, и новая загрузка не заменяет прежние.
    public const string BranchGallery = "branch-gallery";

    /// Картинка новости: её показывают приложение игрока и витрина свободного ПК.
    public const string NewsImage = "news-image";

    /// Фото товара бара — для витрины ПК и меню бара.
    public const string ProductImage = "product-image";

    /// Назначения, у которых объект ровно один: загрузка нового удаляет прежний. Галерея,
    /// новости и товары сюда не входят — иначе второе фото стирало бы первое.
    public static bool IsSingle(string purpose) => purpose is BranchLogo or BranchCover or OrganizationLogo;

    public static bool IsKnown(string? purpose) =>
        purpose is BranchLogo or OrganizationLogo or BranchCover or BranchGallery or NewsImage or ProductImage;
}

/// <summary>
/// Картинки, которые грузит сама платформа (Platform Control), а не клуб: обложки каталога игр и
/// картинки рекламы. Лежат в том же хранилище, в папке `platform/`.
/// </summary>
public static class PlatformMediaPurposeNames
{
    public const string CatalogCover = "catalog-cover";

    public const string AdCreative = "ad-creative";
}

public sealed record PlatformMediaUploadedDto(string Url);

/// <summary>Почему картинку платформы не приняли — машинным словом, фразу строит экран.</summary>
public static class PlatformMediaErrorCodeNames
{
    public const string UnknownPurpose = "media_unknown_purpose";
    public const string FileRequired = "media_file_required";
    public const string StorageNotConfigured = "media_storage_not_configured";
    public const string TooLarge = "media_too_large";
    public const string NotAnImage = "media_not_an_image";
    public const string SteamAppIdInvalid = "steam_app_id_invalid";
    public const string SteamCoverNotFound = "steam_cover_not_found";
}

/// <summary>Обложка игры из Steam по номеру приложения — её ищет и копирует к себе сервер.</summary>
public sealed record SteamCoverRequest(string SteamAppId);

public static class PlatformMediaRoutes
{
    public const string Upload = "/api/platform/media";

    public const string SteamCover = "/api/platform/games/steam-cover";
}
