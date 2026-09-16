namespace AFK4.Platform.Api.Platform.Billing;

public enum BillingOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound
}

/// <param name="Code">
/// Машинное имя отказа (<see cref="Shared.Contracts.Platform.Billing.PlatformErrorCodeNames"/>)
/// для причин, которые человек за панелью исправляет сам. Английскую фразу из
/// <paramref name="Error"/> ему показать нельзя — панель работает на трёх языках.
/// </param>
public sealed record BillingOperationResult<T>(
    BillingOperationStatus Status,
    T? Value,
    string? Error,
    string? Code = null)
    where T : class
{
    public bool Succeeded => Status == BillingOperationStatus.Succeeded;

    public static BillingOperationResult<T> Success(T value) =>
        new(BillingOperationStatus.Succeeded, value, null);

    public static BillingOperationResult<T> BadRequest(string error, string? code = null) =>
        new(BillingOperationStatus.BadRequest, null, error, code);

    public static BillingOperationResult<T> Conflict(string error, string? code = null) =>
        new(BillingOperationStatus.Conflict, null, error, code);

    public static BillingOperationResult<T> NotFound(string error, string? code = null) =>
        new(BillingOperationStatus.NotFound, null, error, code);
}
