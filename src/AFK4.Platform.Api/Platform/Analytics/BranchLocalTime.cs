namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Календарная дата филиала переводится по его часовому поясу, а не по UTC — один экземпляр
/// этого правила на проект. Свёртка суток и отчёт о динамике обязаны сходиться в том, что
/// считается «сегодня» и «вчера» у клуба; второй самостоятельный расчёт того же самого молча
/// разошёлся бы с первым на границе суток.
/// </summary>
public static class BranchLocalTime
{
    public static DateOnly LocalDate(DateTimeOffset instant, TimeZoneInfo zone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, zone).DateTime);

    public static TimeZoneInfo ResolveZone(string timeZoneId)
    {
        // Кривой идентификатор не должен ронять задание/запрос для одного филиала: считаем
        // такой клуб живущим по UTC и идём дальше.
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.Utc;
        }
    }
}
