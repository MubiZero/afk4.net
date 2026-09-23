namespace AFK4.Shared.Contracts.Operator;

/// <summary>
/// Что оператор может найти одной строкой из палитры. Строки, а не enum: контракт переживает
/// клиентов, и старый клиент, встретив незнакомый вид, просто его не покажет.
/// </summary>
public static class BranchSearchKindNames
{
    public const string Seat = "seat";

    public const string Player = "player";

    public const string Reservation = "reservation";

    public const string Receipt = "receipt";

    public const string Order = "order";
}

/// <summary>
/// Одна находка палитры: чем это открыть (<paramref name="Kind"/> и <paramref name="Id"/>) и как
/// узнать глазами (остальное).
/// </summary>
/// <param name="Subtitle">
/// То, чем различают похожие строки: зал у места, телефон у клиента и у брони. Пусто там, где
/// различать нечем.
/// </param>
/// <param name="OccursAtUtc">
/// К какому моменту относится находка: начало брони, дата чека. Сырое время, а не готовая
/// подпись, — язык и часовой пояс знает клиент, а не сервер.
/// </param>
/// <param name="Status">
/// Где находка сейчас, если у неё есть ход жизни: у заказа — новый, готовится, выдан или отменён.
/// Код, а не подпись: подпись на своём языке ставит клиент.
/// </param>
/// <param name="Number">
/// Номер, по которому её называют, когда он не в заголовке: у заказа — номер его чека. По нему
/// человек и узнаёт, что нашлось именно то, что он набирал.
/// </param>
public sealed record BranchSearchResultDto(
    string Kind,
    Guid Id,
    string Title,
    string? Subtitle,
    DateTimeOffset? OccursAtUtc = null,
    long? AmountMinorUnits = null,
    string? CurrencyCode = null,
    string? Status = null,
    string? Number = null);
