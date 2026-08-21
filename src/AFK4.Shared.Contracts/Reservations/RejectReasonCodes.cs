namespace AFK4.Shared.Contracts.Reservations;

/// <summary>
/// Почему клуб отказал в заявке. Код, а не свободный текст: его читает игрок на своём языке, и
/// по нему же видно, отказывает ли филиал по одной и той же причине третий вечер подряд.
///
/// Причин намеренно мало и они про вечер, а не про человека. «Клиент нам не нужен» здесь нет и не
/// будет: локальный запрет игроку — отдельная работа и отдельный разговор, и смешивать его с
/// отказом в конкретной заявке значит прятать запрет за словом «нет мест».
/// </summary>
public static class RejectReasonCodes
{
    /// <summary>Мест на это время не осталось.</summary>
    public const string NoSeats = "no_seats";

    /// <summary>Зал закрыт: техработы, уборка, ремонт.</summary>
    public const string Maintenance = "maintenance";

    /// <summary>В это время в зале закрытое мероприятие.</summary>
    public const string Event = "event";

    /// <summary>Причина своими словами. Без слов бессмысленна — их требует и сервер.</summary>
    public const string Other = "other";

    public static readonly string[] All = [NoSeats, Maintenance, Event, Other];

    public static bool IsSupported(string? code) =>
        code is not null && All.Contains(code, StringComparer.Ordinal);
}
