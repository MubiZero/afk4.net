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

    /// Назначения, у которых объект ровно один: загрузка нового удаляет прежний. Галерея
    /// сюда не входит — иначе второе фото стирало бы первое.
    public static bool IsSingle(string purpose) => purpose is BranchLogo or BranchCover or OrganizationLogo;
    // news-image добавится, когда Новости перейдут на upload (вне этого под-проекта)
}
