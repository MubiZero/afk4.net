using AFK4.Shared.Contracts.Branches;

namespace AFK4.Shared.Contracts.Branding;

/// A club as it appears in the public picker: enough to choose it, nothing about how the
/// business is doing. The mobile app has no hostname to derive a club from, so the player picks
/// one from this list before signing in.
///
/// The showcase fields below (places, price, seats) are what turns a list of names into a
/// shop window: a player picks a club by where it is and what an hour costs, and a name alone
/// answers neither question. They are optional so a club that filled nothing in still appears.
public sealed record OrganizationDirectoryEntryDto(
    Guid OrganizationId,
    string Slug,
    string Name,
    string? LogoUrl,
    string? AccentColor,
    IReadOnlyList<ClubPlaceDto>? Places = null,
    long? PricePerHourFromMinorUnits = null,
    string? CurrencyCode = null,
    int SeatCount = 0,
    double? Rating = null,
    int ReviewCount = 0);

/// A physical club of the network: what the card shows and where the map puts its pin.
public sealed record ClubPlaceDto(
    Guid BranchId,
    string Name,
    string City,
    string? Address,
    string? Description,
    string? CoverImageUrl,
    double? Latitude,
    double? Longitude,
    /// Расписание клуба: игрок хочет знать не только где клуб, но и открыт ли он сейчас.
    /// Пустой список — расписание не задано.
    IReadOnlyList<BranchWorkingHoursDayDto>? WorkingHours = null);
