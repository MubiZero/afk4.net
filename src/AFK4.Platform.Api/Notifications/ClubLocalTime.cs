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
    public static string At(DateTimeOffset instant, string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return instant.ToString("HH:mm");
        }

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            return TimeZoneInfo.ConvertTime(instant, zone).ToString("HH:mm");
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return instant.ToString("HH:mm");
        }
    }
}
