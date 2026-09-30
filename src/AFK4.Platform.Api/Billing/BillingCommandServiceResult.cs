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

    public static BillingCommandServiceResult<TResponse> RequestConflict(string error, string? code = null) =>
        new(false, true, false, error, default, code);

    public static BillingCommandServiceResult<TResponse> Missing(string error) => new(false, false, true, error, default);

    public static BillingCommandServiceResult<TResponse> Invalid(string error, string? code = null) =>
        new(false, false, false, error, default, MachineErrorCode.Resolve(error, code));
}

/// <summary>
/// Сервисы давно называют причину отказа прямо в <c>error</c> («open_shift_required»), а поле
/// <c>code</c> оставляли пустым: клиент, который читает именно <c>code</c>, видел null и говорил
/// «сервер не принял данные», хотя причина была названа. Фабрики отказов берут код из самого
/// error, когда тот — машинное имя, а не английская фраза, и не заставляют каждый вызов
/// повторять одно и то же дважды.
/// </summary>
public static class MachineErrorCode
{
    public static string? Resolve(string? error, string? code) =>
        code ?? (IsMachineName(error) ? error : null);

    private static bool IsMachineName(string? value) =>
        !string.IsNullOrEmpty(value) && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');
}
