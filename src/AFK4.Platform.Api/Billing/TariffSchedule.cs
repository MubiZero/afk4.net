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
    /// Предел проверяемого промежутка — неделя. Расписание повторяется по неделям, а сессии и
    /// брони такой длины не бывает; более длинный промежуток отклоняется, а не проверяется по
    /// минуте.
    /// </summary>
    private const int MaxCheckedMinutes = 7 * MinutesPerDay;

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
    /// Действует ли тариф на протяжении ВСЕГО промежутка [<paramref name="startsAtUtc"/>,
    /// <paramref name="endsAtUtc"/>).
    ///
    /// Одной проверки момента начала мало. Сессия и бронь считаются одной ставкой на всю
    /// длительность, поэтому тариф, действующий только в момент старта, растягивает утреннюю цену
    /// на весь вечер: бронь с 08:00 до 23:00 на утреннем тарифе иначе проходит целиком по утренней
    /// цене. Впустить в окно и не проверить выход — это отдать разницу даром.
    /// </summary>
    public static bool AppliesThroughout(
        int daysMask,
        int? fromMinuteOfDay,
        int? toMinuteOfDay,
        DateTimeOffset startsAtUtc,
        DateTimeOffset endsAtUtc,
        TimeZoneInfo zone)
    {
        if (!IsRestricted(daysMask, fromMinuteOfDay, toMinuteOfDay))
        {
            return true;
        }

        // Конца нет (или он не позже начала) — проверить можно только сам момент. Так приходит
        // сессия без запланированного конца: она длится «пока играет», и её остаток неизвестен.
        if (endsAtUtc <= startsAtUtc)
        {
            return AppliesAt(daysMask, fromMinuteOfDay, toMinuteOfDay, startsAtUtc, zone);
        }

        if ((endsAtUtc - startsAtUtc).TotalMinutes > MaxCheckedMinutes)
        {
            return false;
        }

        // Шаг в минуту: границы окна заданы минутами, дыр короче минуты в расписании не бывает.
        for (var cursor = startsAtUtc; cursor < endsAtUtc; cursor = cursor.AddMinutes(1))
        {
            if (!AppliesAt(daysMask, fromMinuteOfDay, toMinuteOfDay, cursor, zone))
            {
                return false;
            }
        }

        // Последнее мгновение промежутка проверяется отдельно: шаг в минуту мог перешагнуть конец
        // окна внутри последней минуты.
        return AppliesAt(daysMask, fromMinuteOfDay, toMinuteOfDay, endsAtUtc.AddTicks(-1), zone);
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
