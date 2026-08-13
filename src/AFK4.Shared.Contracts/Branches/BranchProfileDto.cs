namespace AFK4.Shared.Contracts.Branches;

public sealed record BranchProfileDto(
    Guid OrganizationId,
    Guid BranchId,
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
    string? CoverImageUrl,
    Guid? CoverMediaId,
    double? Latitude,
    double? Longitude,
    string TimeZone,
    string Locale,
    IReadOnlyList<BranchWorkingHoursDayDto> WorkingHours,
    DateTimeOffset CreatedAtUtc);
