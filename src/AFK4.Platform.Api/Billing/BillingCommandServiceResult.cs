namespace AFK4.Platform.Api.Billing;

public sealed record BillingCommandServiceResult<TResponse>(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    TResponse? Response)
{
    public static BillingCommandServiceResult<TResponse> Ok(TResponse response) => new(true, false, false, null, response);

    public static BillingCommandServiceResult<TResponse> RequestConflict(string error) => new(false, true, false, error, default);

    public static BillingCommandServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);

    public static BillingCommandServiceResult<TResponse> Invalid(string error) => new(false, false, false, error, default);
}
