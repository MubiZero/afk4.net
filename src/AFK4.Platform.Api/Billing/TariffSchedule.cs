namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Когда действует тариф.
///
/// Пустые утренние часы продаются дешевле не окнами внутри одного тарифа, а отдельным тарифом с
/// расписанием. Сессия считается одной ставкой на всю длительность — минимальное оплачиваемое
/// время и шаг округления применяются к ней один раз, — и окна внутри тарифа заставили бы резать
/// отыгранное время на куски и заново решать, к чему из этого относятся минимум и округление.
/// Тот же расчёт живёт ещё в живом счётчике стоимости и в цене брони, и расходиться этим трём
/// нельзя. Отдельный тариф не трогает расчёт вообще: он лишь отвечает на вопрос «можно ли сейчас
/// выбрать этот тариф».
///
/// Автоматически подставлять тариф не требуется: достаточно не давать выбрать тот, который на
/// нужное время не действует. Утренний просто не предлагается в восемь вечера, а весь остальной
/// путь выбора остаётся прежним.
///
/// Проверяется только момент старта — решение владельца 2026-09-23. Сессия и бронь до конца
/// считаются по тарифу, на котором начались, и продление тоже: игрок сел в 15:30 на тариф
/// 08:00–16:00 — утренняя цена за всё время. Иначе клубу пришлось бы объяснять, почему две
/// одинаковые сессии стоят по-разному, а «без конца» можно, а «на два часа» нельзя. Кто хочет
/// отдельную цену на вечер, заводит вечерний тариф или пакет со своим окном.
/// </summary>
public static class TariffSchedule
{
    /// <summary>Машинный код: тариф существует, но на это время не действует.</summary>
    public const string OutsideHoursCode = "tariff_outside_its_hours";

    /// <summary>Машинный код: расписание задано неверно.</summary>
    public const string InvalidScheduleCode = "invalid_tariff_schedule";

    /// <summary>Каждый день — оно же значение у тарифов, заведённых до расписаний.</summary>
    public const int EveryDayMask = 0;

    public const int AllDaysMask = 0b111_1111;

    private const int MinutesPerDay = 24 * 60;

    /// <summary>
    /// Действует ли тариф в этот момент по местному времени филиала.
    /// </summary>
    public static bool AppliesAt(
        int daysMask,
        int? fromMinuteOfDay,
        int? toMinuteOfDay,
        DateTimeOffset instant,
        TimeZoneInfo zone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, zone);
        var minuteOfDay = local.Hour * 60 + local.Minute;

        // Часы не заданы (или заданы нулевым окном, которое запись отклоняет) — тариф действует
        // круглые сутки в отмеченные дни.
        if (fromMinuteOfDay is not int from || toMinuteOfDay is not int to || from == to)
        {
            return MatchesDay(daysMask, local.DayOfWeek);
        }

        if (from < to)
        {
            return MatchesDay(daysMask, local.DayOfWeek) && minuteOfDay >= from && minuteOfDay < to;
        }

        // Окно через полночь принадлежит тому дню, в который оно началось: ночной тариф «с
        // понедельника 22:00» работает и в час ночи вторника, а вторник сам по себе отмечать для
        // этого не надо — иначе владелец, отметив только рабочие дни, потерял бы ночь с пятницы.
        return (minuteOfDay >= from && MatchesDay(daysMask, local.DayOfWeek))
            || (minuteOfDay < to && MatchesDay(daysMask, Previous(local.DayOfWeek)));
    }

    /// <summary>
    /// Когда тариф откроется в ближайший раз, начиная с <paramref name="instant"/>: экран выбора
    /// показывает «Утренний — с 08:00» вместо того, чтобы молча прятать его вечером. Если тариф
    /// действует уже сейчас — сам момент; <c>null</c> — в ближайшую неделю не откроется.
    /// </summary>
    public static DateTimeOffset? NextStartUtc(
        int daysMask,
        int? fromMinuteOfDay,
        int? toMinuteOfDay,
        DateTimeOffset instant,
        TimeZoneInfo zone)
    {
        if (AppliesAt(daysMask, fromMinuteOfDay, toMinuteOfDay, instant, zone))
        {
            return instant;
        }

        var hasHours = fromMinuteOfDay is int from && toMinuteOfDay is int to && from != to;
        var startMinute = hasHours ? fromMinuteOfDay!.Value : 0;
        var localToday = TimeZoneInfo.ConvertTime(instant, zone).Date;
        for (var dayOffset = 0; dayOffset <= 7; dayOffset++)
        {
            var localDate = localToday.AddDays(dayOffset);
            if (!MatchesDay(daysMask, localDate.DayOfWeek))
            {
                continue;
            }

            var localStart = DateTime.SpecifyKind(localDate.AddMinutes(startMinute), DateTimeKind.Unspecified);
            var startUtc = new DateTimeOffset(localStart, zone.GetUtcOffset(localStart));
            if (startUtc > instant)
            {
                return startUtc;
            }
        }

        return null;
    }

    /// <summary>
    /// Проверяет расписание при записи. Возвращает машинный код ошибки или <c>null</c>.
    /// </summary>
    public static string? Validate(int daysMask, int? fromMinuteOfDay, int? toMinuteOfDay)
    {
        if (daysMask is < 0 or > AllDaysMask)
        {
            return InvalidScheduleCode;
        }

        // Одна половина окна без второй — это не «с восьми утра и до упора», а недописанная
        // настройка, и догадываться за владельца о цене его же часов не следует.
        if (fromMinuteOfDay is null != (toMinuteOfDay is null))
        {
            return InvalidScheduleCode;
        }

        if (fromMinuteOfDay is not int from || toMinuteOfDay is not int to)
        {
            return null;
        }

        if (from is < 0 or >= MinutesPerDay || to is < 0 or >= MinutesPerDay)
        {
            return InvalidScheduleCode;
        }

        // Совпадающие границы читаются и как «круглые сутки», и как «нисколько». Обе догадки
        // ошибаются в цене, поэтому такое окно не принимается вовсе.
        return from == to ? InvalidScheduleCode : null;
    }

    /// <summary>Есть ли у тарифа расписание вообще — круглосуточный ежедневный не показывают.</summary>
    public static bool IsRestricted(int daysMask, int? fromMinuteOfDay, int? toMinuteOfDay) =>
        (daysMask != EveryDayMask && daysMask != AllDaysMask) ||
        (fromMinuteOfDay is int from && toMinuteOfDay is int to && from != to);

    private static bool MatchesDay(int daysMask, DayOfWeek day) =>
        daysMask == EveryDayMask || (daysMask & (1 << BitIndex(day))) != 0;

    // Неделя считается с понедельника: так её видят и клуб, и его посетители.
    private static int BitIndex(DayOfWeek day) => ((int)day + 6) % 7;

    private static DayOfWeek Previous(DayOfWeek day) => (DayOfWeek)(((int)day + 6) % 7);
}
