using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Install;

/// <param name="IssuedByStaffUserId">Кто выдал код — для журнала: сам он при установке не присутствовал.</param>
/// <param name="NewDevice">ПК встал впервые и потратил одну установку кода.</param>
public sealed record InstallCodeEnrollment(
    InstallEnrollResponse Response,
    Guid InstallCodeId,
    Guid IssuedByStaffUserId,
    bool NewDevice);

/// <param name="Code">
/// Машинное имя отказа для причин, которые мастер установки обязан назвать человеку своими
/// словами («на это место уже привязан другой ПК»). Английская фраза из <paramref name="Error"/>
/// для этого не годится: мастер работает и по-русски, и по-таджикски. Для <see cref="InstallOperationStatus.Forbidden"/>
/// здесь лежит статус организации (см. <see cref="Reason"/>), а не код отказа.
/// </param>
/// <param name="Reason">Причина приостановки организации — только вместе с <see cref="InstallOperationStatus.Forbidden"/>.</param>
public sealed record InstallOperationResult<T>(
    InstallOperationStatus Status,
    T? Value,
    string? Error,
    Guid? OrganizationId = null,
    Guid? BranchId = null,
    Guid? StaffUserId = null,
    string? Code = null,
    string? Reason = null)
{
    public bool Succeeded => Status == InstallOperationStatus.Succeeded;

    public static InstallOperationResult<T> Success(
        T value,
        Guid organizationId,
        Guid? branchId,
        Guid? staffUserId = null) =>
        new(InstallOperationStatus.Succeeded, value, null, organizationId, branchId, staffUserId);

    public static InstallOperationResult<T> BadRequest(
        string error,
        Guid? organizationId = null,
        Guid? branchId = null,
        Guid? staffUserId = null,
        string? code = null) =>
        new(InstallOperationStatus.BadRequest, default, error, organizationId, branchId, staffUserId, code);

    public static InstallOperationResult<T> NotFound(string error) =>
        new(InstallOperationStatus.NotFound, default, error);

    public static InstallOperationResult<T> Conflict(string error, Guid organizationId, Guid branchId, string? code = null) =>
        new(InstallOperationStatus.Conflict, default, error, organizationId, branchId, StaffUserId: null, Code: code);

    /// <summary>
    /// Организация приостановлена или удаляется. Тот же контракт, что у 403 <c>OrganizationSuspended</c>
    /// остальных маршрутов ПК (<see cref="AFK4.Platform.Api.Platform.Tenancy.OrganizationStatusGuardExtensions.RequireActiveAsync"/>):
    /// одна и та же пара «статус организации + причина», а не второй отдельно придуманный отказ.
    /// </summary>
    public static InstallOperationResult<T> Suspended(
        string organizationStatus,
        string? reason,
        Guid organizationId,
        Guid? branchId = null,
        Guid? staffUserId = null) =>
        new(InstallOperationStatus.Forbidden, default, "OrganizationSuspended", organizationId, branchId, staffUserId, organizationStatus, reason);
}

public enum InstallOperationStatus
{
    Succeeded,
    BadRequest,
    NotFound,
    Conflict,
    Forbidden
}
