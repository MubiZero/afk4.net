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

public sealed record PlatformAnalyticsOverviewDto(
    DateTimeOffset GeneratedAtUtc,
    string CurrencyCode,
    IReadOnlyList<AnalyticsMonthDto> Months,
    long CurrentMrrMinorUnits,
    int CurrentPayingClubs,
    long AverageRevenuePerClubMinorUnits,
    long OutstandingMinorUnits);
