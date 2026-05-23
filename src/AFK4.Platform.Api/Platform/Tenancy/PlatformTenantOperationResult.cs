namespace AFK4.Platform.Api.Platform.Tenancy;

public enum PlatformTenantOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound
}

public sealed record PlatformTenantOperationResult<T>(
    PlatformTenantOperationStatus Status,
    T? Value,
    string? Error)
    where T : class
{
    public bool Succeeded => Status == PlatformTenantOperationStatus.Succeeded;

    public static PlatformTenantOperationResult<T> Success(T value) =>
        new(PlatformTenantOperationStatus.Succeeded, value, null);

    public static PlatformTenantOperationResult<T> BadRequest(string error) =>
        new(PlatformTenantOperationStatus.BadRequest, null, error);

    public static PlatformTenantOperationResult<T> Conflict(string error) =>
        new(PlatformTenantOperationStatus.Conflict, null, error);

    public static PlatformTenantOperationResult<T> NotFound(string error) =>
        new(PlatformTenantOperationStatus.NotFound, null, error);
}
