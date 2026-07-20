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
    string? LogoUrl,
    Guid? LogoMediaId,
    string TimeZone,
    string Locale,
    IReadOnlyList<BranchWorkingHoursDayDto> WorkingHours,
    DateTimeOffset CreatedAtUtc);
