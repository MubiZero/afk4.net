namespace AFK4.Platform.Api.Search;

/// <summary>
/// Телефоны в базе записаны так, как их диктуют: «+992 90 012-34-56». Ищут их иначе — подряд
/// цифрами. Без нормализации поиск по номеру не находил бы ничего, поэтому сравниваются цифры:
/// запрос чистит <see cref="Digits"/>, а хранимый номер — цепочка <c>Replace</c> прямо в запросе
/// к базе (её EF переводит в SQL, а регулярное выражение — нет).
/// </summary>
public static class PhoneQuery
{
    // Три цифры — порог, ниже которого «номер» перестаёт сужать поиск: «99» есть в половине
    // номеров страны, и выдача превратилась бы в шум.
    private const int MinimumDigits = 3;

    /// <summary>
    /// Цифры из набранного, если набирали похоже на номер: «+992 90 012» → «99290012». Для
    /// «PC-12» вернёт пусто — это имя места, а не телефон, и вытаскивать по нему всех, у кого в
    /// номере есть «12», значит топить в шуме то, что человек искал.
    /// </summary>
    public static string Digits(string query)
    {
        if (query.Any(char.IsLetter))
        {
            return string.Empty;
        }

        var digits = new string([.. query.Where(char.IsDigit)]);
        return digits.Length >= MinimumDigits ? digits : string.Empty;
    }
}
