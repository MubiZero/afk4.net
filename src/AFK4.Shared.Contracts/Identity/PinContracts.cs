namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Новый сетевой PIN. Старый здесь не спрашивается намеренно: человек уже вошёл в приложение, а
/// потребовать старое значило бы запереть выход ровно тому, кто его забыл. Именно это и делает
/// приложение единственным местом, где PIN задают, — ни одной SMS на это не тратится.
/// </summary>
public sealed record SetMyPinRequest(string Pin);

/// <summary>
/// Форма PIN — 4–8 цифр. Живёт в контрактах, потому что то же правило показывает приложение,
/// прежде чем звать сервер: два разных представления о длине PIN разъехались бы на первой правке.
/// </summary>
public static class PinFormat
{
    public const int MinLength = 4;

    public const int MaxLength = 8;

    public static bool IsWellFormed(string? pin) =>
        pin is not null
        && pin.Length is >= MinLength and <= MaxLength
        && pin.All(character => character is >= '0' and <= '9');
}
