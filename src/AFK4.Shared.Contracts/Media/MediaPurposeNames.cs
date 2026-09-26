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
