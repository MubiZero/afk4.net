using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Install;

public interface IInstallService
{
    /// <param name="staffUserId">Кто ставит: сам себя он в списке сотрудников зала не считает.</param>
    Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverForStaffAsync(
        Guid organizationId,
        IReadOnlySet<Guid> branchIds,
        string ownerDisplayName,
        Guid staffUserId,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallCreateSeatResponse>> CreateSeatForStaffAsync(
        Guid organizationId,
        Guid? staffUserId,
        AuthenticatedInstallCreateSeatRequest request,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallEnrollResponse>> EnrollForStaffAsync(
        Guid organizationId,
        AuthenticatedInstallEnrollRequest request,
        CancellationToken cancellationToken);
}

/// <param name="Code">
/// Машинное имя отказа для причин, которые мастер установки обязан назвать человеку своими
/// словами («на это место уже привязан другой ПК»). Английская фраза из <paramref name="Error"/>
/// для этого не годится: мастер работает и по-русски, и по-таджикски.
/// </param>
public sealed record InstallOperationResult<T>(
    InstallOperationStatus Status,
    T? Value,
    string? Error,
    Guid? OrganizationId = null,
    Guid? BranchId = null,
    Guid? StaffUserId = null,
    string? Code = null)
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
        Guid? staffUserId = null) =>
        new(InstallOperationStatus.BadRequest, default, error, organizationId, branchId, staffUserId);

    public static InstallOperationResult<T> NotFound(string error) =>
        new(InstallOperationStatus.NotFound, default, error);

    public static InstallOperationResult<T> Conflict(string error, Guid organizationId, Guid branchId, string? code = null) =>
        new(InstallOperationStatus.Conflict, default, error, organizationId, branchId, StaffUserId: null, Code: code);
}

public enum InstallOperationStatus
{
    Succeeded,
    BadRequest,
    NotFound,
    Conflict
}
