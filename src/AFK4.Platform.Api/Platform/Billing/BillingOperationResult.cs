namespace AFK4.Platform.Api.Platform.Billing;

public enum BillingOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound
}

public sealed record BillingOperationResult<T>(
    BillingOperationStatus Status,
    T? Value,
    string? Error)
    where T : class
{
    public bool Succeeded => Status == BillingOperationStatus.Succeeded;

    public static BillingOperationResult<T> Success(T value) =>
        new(BillingOperationStatus.Succeeded, value, null);

    public static BillingOperationResult<T> BadRequest(string error) =>
        new(BillingOperationStatus.BadRequest, null, error);

    public static BillingOperationResult<T> Conflict(string error) =>
        new(BillingOperationStatus.Conflict, null, error);

    public static BillingOperationResult<T> NotFound(string error) =>
        new(BillingOperationStatus.NotFound, null, error);
}
