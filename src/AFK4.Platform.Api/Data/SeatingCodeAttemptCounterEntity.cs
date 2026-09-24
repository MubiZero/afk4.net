namespace AFK4.Platform.Api.Data;

/// <summary>
/// Счёт неверных кодов посадки в текущем окне — на человека и на весь клуб.
///
/// Кодов у клуба столько, сколько свободных ПК, а вариантов — миллион: угадать можно только
/// перебором, и перебор упирается сюда. Отдельной таблицей, а не колонками личности и
/// организации: это учёт попыток, а не свойство ни той, ни другой.
/// </summary>
public sealed class SeatingCodeAttemptCounterEntity
{
    /// <summary>Одно из <see cref="SeatingCodeAttemptScopes"/>.</summary>
    public string Scope { get; set; } = string.Empty;

    /// <summary>Личность или счёт игрока — для «player»; организация — для «organization».</summary>
    public Guid ScopeId { get; set; }

    public int FailedCount { get; set; }

    public DateTimeOffset WindowStartedAtUtc { get; set; }
}

public static class SeatingCodeAttemptScopes
{
    public const string Player = "player";

    public const string Organization = "organization";
}
