using AFK4.Shared.Contracts.Tournaments;

namespace AFK4.Platform.Api.Tournaments;

/// <summary>
/// Исход операции. `NotFound` отдельно от отказа: «такого события нет» и «записаться нельзя» —
/// разные новости и разные ответы HTTP.
/// </summary>
public sealed record TournamentResult<T>(bool Succeeded, bool NotFound, string? Error, T? Value)
{
    public static TournamentResult<T> Ok(T value) => new(true, false, null, value);

    public static TournamentResult<T> Missing() => new(false, true, null, default);

    /// <param name="error">Код из <see cref="TournamentRefusalCodes"/> — фразу собирает клиент.</param>
    public static TournamentResult<T> Refused(string error) => new(false, false, error, default);
}
