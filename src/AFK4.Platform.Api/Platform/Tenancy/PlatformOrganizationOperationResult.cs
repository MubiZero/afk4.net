using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Tenancy;

public enum PlatformOrganizationOperationStatus
{
    Succeeded,
    BadRequest,
    Conflict,
    NotFound,
    PlanLimitReached
}

/// <param name="Code">
/// Машинное имя отказа (<see cref="Shared.Contracts.Platform.Billing.PlatformErrorCodeNames"/>)
/// для причин, которые человек за панелью исправляет сам: занятый адрес клуба, занятый логин
/// владельца. Английскую фразу из <paramref name="Error"/> панель показать не может.
/// </param>
public sealed record PlatformOrganizationOperationResult<T>(
    PlatformOrganizationOperationStatus Status,
    T? Value,
    string? Error,
    PlanLimitExceededDto? PlanLimit = null,
    string? Code = null)
    where T : class
{
    public bool Succeeded => Status == PlatformOrganizationOperationStatus.Succeeded;

    public static PlatformOrganizationOperationResult<T> Success(T value) =>
        new(PlatformOrganizationOperationStatus.Succeeded, value, null);

    public static PlatformOrganizationOperationResult<T> BadRequest(string error, string? code = null) =>
        new(PlatformOrganizationOperationStatus.BadRequest, null, error, PlanLimit: null, Code: code);

    public static PlatformOrganizationOperationResult<T> Conflict(string error, string? code = null) =>
        new(PlatformOrganizationOperationStatus.Conflict, null, error, PlanLimit: null, Code: code);

    public static PlatformOrganizationOperationResult<T> NotFound(string error) =>
        new(PlatformOrganizationOperationStatus.NotFound, null, error);

    public static PlatformOrganizationOperationResult<T> PlanLimitReached(PlanLimitExceededDto planLimit) =>
        new(PlatformOrganizationOperationStatus.PlanLimitReached, default, "Plan branch limit has been reached.", planLimit);
}
