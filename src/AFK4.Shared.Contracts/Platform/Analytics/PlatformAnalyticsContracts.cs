namespace AFK4.Shared.Contracts.Platform.Analytics;

/// <summary>
/// Точка помесячного ряда. Год и месяц едут числами: название месяца — дело клиента,
/// у которого есть язык пользователя.
/// </summary>
public sealed record AnalyticsMonthDto(
    int Year,
    int Month,
    long RecurringMinorUnits,
    long OneOffMinorUnits,
    int Joined,
    int Left,
    int PayingAtMonthEnd);

/// <summary>
/// Насколько далеко зашёл переход на сетевой PIN. <see cref="ActivePlayers"/> — все, кто приходил
/// за окно; <see cref="ActivePlayersWithIdentity"/> — те из них, кто вообще может задать PIN, то
/// есть чья карточка подшита к личности. Доля считается по второму числу: гость, заведённый на
/// стойке, PIN задать не может, и включать его в знаменатель значит сделать порог недостижимым.
/// </summary>
public sealed record PinAdoptionDto(
    DateTimeOffset GeneratedAtUtc,
    int WindowDays,
    int ActivePlayers,
    int ActivePlayersWithIdentity,
    int ActivePlayersWithPin,
    int AdoptionPercent);

public sealed record PlatformAnalyticsOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    string CurrencyCode,
    IReadOnlyList<AnalyticsMonthDto> Months,
    long CurrentMrrMinorUnits,
    int CurrentPayingClubs,
    long AverageRevenuePerClubMinorUnits,
    long OutstandingMinorUnits);
