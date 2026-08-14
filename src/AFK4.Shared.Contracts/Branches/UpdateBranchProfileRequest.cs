namespace AFK4.Shared.Contracts.Branches;

public sealed record UpdateBranchProfileRequest(
    Guid OrganizationId,
    string Name,
    string City,
    string? Description,
    string? Address,
    string? Phone,
    string? Telegram,
    string? Website,
    string? Instagram,
    string? LogoUrl,
    Guid? LogoMediaId,
    string TimeZone,
    string Locale,
    IReadOnlyList<BranchWorkingHoursDayDto> WorkingHours,
    // Витрина клуба в приложении игрока: фото зала и точка на карте. В конце списка и с
    // умолчаниями — чтобы старый клиент, который их не шлёт, продолжал сохранять профиль.
    string? CoverImageUrl = null,
    Guid? CoverMediaId = null,
    IReadOnlyList<BranchPhotoDto>? Photos = null,
    double? Latitude = null,
    double? Longitude = null);
