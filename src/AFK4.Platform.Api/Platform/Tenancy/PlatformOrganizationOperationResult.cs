namespace AFK4.Platform.Api.Platform.Tenancy;

public enum PlatformOrganizationOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound
}

public sealed record PlatformOrganizationOperationResult<T>(
    PlatformOrganizationOperationStatus Status,
    T? Value,
    string? Error)
    where T : class
{
    public bool Succeeded => Status == PlatformOrganizationOperationStatus.Succeeded;

    public static PlatformOrganizationOperationResult<T> Success(T value) =>
        new(PlatformOrganizationOperationStatus.Succeeded, value, null);

    public static PlatformOrganizationOperationResult<T> BadRequest(string error) =>
        new(PlatformOrganizationOperationStatus.BadRequest, null, error);

    public static PlatformOrganizationOperationResult<T> Conflict(string error) =>
        new(PlatformOrganizationOperationStatus.Conflict, null, error);

    public static PlatformOrganizationOperationResult<T> NotFound(string error) =>
        new(PlatformOrganizationOperationStatus.NotFound, null, error);
}
