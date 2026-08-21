namespace AFK4.Shared.Contracts.Sessions;

/// <summary>
/// С чего началась сессия. Раньше на этот вопрос отвечали догадкой по косвенным признакам —
/// «раз есть бронь, значит по брони», — и догадка врала на любом нестандартном вечере.
///
/// Пустая строка — законный ответ «неизвестно» для строк, заведённых до того, как вопрос начали
/// задавать. Подставлять вместо неё <see cref="Operator"/> нельзя: это уже утверждение, а не факт.
/// </summary>
public static class SessionOriginNames
{
    /// <summary>Посадил администратор за стойкой.</summary>
    public const string Operator = "operator";

    /// <summary>Игрок сел сам, назвав номер и сетевой PIN за игровым ПК.</summary>
    public const string PlayerPin = "player_pin";

    /// <summary>Сессия выросла из брони: человек пришёл на забронированное время.</summary>
    public const string Reservation = "reservation";

    public static readonly string[] All = [Operator, PlayerPin, Reservation];

    public static bool IsSupported(string? origin) =>
        origin is not null && All.Contains(origin, StringComparer.Ordinal);
}
