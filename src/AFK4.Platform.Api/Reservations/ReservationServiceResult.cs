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

    public static ReservationServiceResult<TResponse> Invalid(string error) => new(false, false, false, error, default);
}
