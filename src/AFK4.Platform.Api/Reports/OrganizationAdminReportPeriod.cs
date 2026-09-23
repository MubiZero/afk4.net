namespace AFK4.Platform.Api.Reports;

public sealed record OrganizationAdminReportPeriod(
    DateOnly FromDate,
    DateOnly ToDate,
    string TimeZone,
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc)
{
    public static OrganizationAdminReportPeriod Resolve(DateOnly fromDate, DateOnly toDate, string timeZoneId)
    {
        if (toDate < fromDate)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "The report end date must not precede the start date.");
        }

        if (toDate.DayNumber - fromDate.DayNumber + 1 > 366)
        {
            throw new ArgumentOutOfRangeException(nameof(toDate), "The report range cannot exceed 366 days.");
        }

        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var fromUtc = ResolveUtcMidnight(fromDate, timeZone);
        var toUtc = ResolveUtcMidnight(toDate.AddDays(1), timeZone);
        return new OrganizationAdminReportPeriod(fromDate, toDate, timeZone.Id, fromUtc, toUtc);
    }

    /// <summary>
    /// Период, с которым сравнивается выбранный: столько же дней, вплотную перед ним. Одно правило
    /// и для экрана, и для выгрузки, чтобы строка сравнения в файле называла те же даты.
    /// </summary>
    public static (DateOnly FromDate, DateOnly ToDate) PreviousOf(DateOnly fromDate, DateOnly toDate)
    {
        var previousTo = fromDate.AddDays(-1);
        return (previousTo.AddDays(-(toDate.DayNumber - fromDate.DayNumber)), previousTo);
    }

    private static DateTimeOffset ResolveUtcMidnight(DateOnly date, TimeZoneInfo timeZone)
    {
        var localMidnight = DateTime.SpecifyKind(date.ToDateTime(TimeOnly.MinValue), DateTimeKind.Unspecified);
        return new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(localMidnight, timeZone), TimeSpan.Zero);
    }
}
