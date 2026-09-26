namespace AFK4.Platform.Api.Notifications;

/// <summary>
/// Время так, как его называет клуб.
///
/// Игроку пишут про его вечер, а не про UTC: «ждём в 19:00» должно совпадать с тем, что он
/// прочитал в приложении и сказал друзьям. Неизвестная зона в профиле клуба — не повод не
/// написать вовсе: тогда время называется как есть.
/// </summary>
public static class ClubLocalTime
{
    /// <summary>Какой сейчас день у клуба: день рождения и «сегодня» считаются по его календарю.</summary>
    public static DateOnly Today(DateTimeOffset instant, string? timeZoneId) =>
        DateOnly.FromDateTime(Local(instant, timeZoneId).DateTime);

    public static string At(DateTimeOffset instant, string? timeZoneId) => Local(instant, timeZoneId).ToString("HH:mm");

    private static DateTimeOffset Local(DateTimeOffset instant, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return instant;
        }

        try
        {
            return TimeZoneInfo.ConvertTime(instant, TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return instant;
        }
    }
}
