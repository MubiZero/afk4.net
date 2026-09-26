namespace AFK4.Platform.Api.Players;

/// <summary>
/// День рождения игрока: проверка введённой даты, возраст и день, когда поздравлять. Всё — в днях
/// календаря клуба, а не в UTC: в Душанбе день рождения наступает на пять часов раньше, чем в UTC.
/// </summary>
public static class PlayerBirthdays
{
    /// <summary>Младше — это ошибка ввода, а не игрок клуба; старше — тоже.</summary>
    public const int MinAge = 5;
    public const int MaxAge = 110;

    /// <summary>Код ошибки для приложения; null — дата годится.</summary>
    public static string? Validate(DateOnly birthDate, DateOnly today) =>
        birthDate > today || AgeOn(birthDate, today) is < MinAge or > MaxAge ? "invalid_birth_date" : null;

    /// <summary>Полных лет на этот день.</summary>
    public static int AgeOn(DateOnly birthDate, DateOnly day)
    {
        var age = day.Year - birthDate.Year;
        return day < BirthdayIn(day.Year, birthDate) ? age - 1 : age;
    }

    /// <summary>День рождения в этом году. Родился 29 февраля — в невисокосный год поздравляем 28-го.</summary>
    public static DateOnly BirthdayIn(int year, DateOnly birthDate) =>
        birthDate is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(year)
            ? new DateOnly(year, 2, 28)
            : new DateOnly(year, birthDate.Month, birthDate.Day);
}
