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
public sealed record BranchSearchResultDto(
    string Kind,
    Guid Id,
    string Title,
    string? Subtitle,
    DateTimeOffset? OccursAtUtc = null,
    long? AmountMinorUnits = null,
    string? CurrencyCode = null);
