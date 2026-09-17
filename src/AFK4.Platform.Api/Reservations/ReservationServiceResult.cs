namespace AFK4.Platform.Api.Reservations;

public sealed record ReservationServiceResult<TResponse>(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    TResponse? Response,
    string? Code = null,
    int? CurrentVersion = null)
{
    public static ReservationServiceResult<TResponse> Ok(TResponse response) => new(true, false, false, null, response);

    public static ReservationServiceResult<TResponse> RequestConflict(
        string error,
        string? code = null,
        int? currentVersion = null) =>
        new(false, true, false, error, default, code, currentVersion);

    public static ReservationServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);

    /// <param name="code">
    /// Машинное имя отказа. Без него у стойки остаётся английская фраза из <paramref name="error"/>,
    /// которую нельзя показать ни по-русски, ни по-таджикски.
    /// </param>
    public static ReservationServiceResult<TResponse> Invalid(string error, string? code = null) =>
        new(false, false, false, error, default, code);
}
