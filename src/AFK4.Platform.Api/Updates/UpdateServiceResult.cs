namespace AFK4.Platform.Api.Updates;

public sealed record UpdateServiceResult<TResponse>(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    TResponse? Response)
{
    public static UpdateServiceResult<TResponse> Ok(TResponse response) => new(true, false, false, null, response);

    public static UpdateServiceResult<TResponse> RequestConflict(string error) => new(false, true, false, error, default);

    public static UpdateServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);

    public static UpdateServiceResult<TResponse> Invalid(string error) => new(false, false, false, error, default);
}
