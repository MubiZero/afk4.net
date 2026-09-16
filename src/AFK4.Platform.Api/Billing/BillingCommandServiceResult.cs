namespace AFK4.Platform.Api.Billing;

/// <param name="Code">
/// Машинное имя отказа для тех причин, которые клиент должен назвать своими словами
/// («тариф с таким именем уже есть»). Без него остаётся только английская фраза из
/// <paramref name="Error"/> — её нельзя показать человеку в русском или таджикском окне.
/// Пусто там, где причина ещё не названа кодом; клиент тогда говорит общими словами.
/// </param>
public sealed record BillingCommandServiceResult<TResponse>(
    bool Succeeded,
    bool Conflict,
    bool NotFound,
    string? Error,
    TResponse? Response,
    string? Code = null)
{
    public static BillingCommandServiceResult<TResponse> Ok(TResponse response) => new(true, false, false, null, response);

    public static BillingCommandServiceResult<TResponse> RequestConflict(string error) => new(false, true, false, error, default);

    public static BillingCommandServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);

    public static BillingCommandServiceResult<TResponse> Invalid(string error, string? code = null) =>
        new(false, false, false, error, default, code);
}
