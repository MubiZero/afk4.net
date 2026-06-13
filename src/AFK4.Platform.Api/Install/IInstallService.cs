using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Install;

public interface IInstallService
{
    Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverForStaffAsync(
        Guid organizationId,
        IReadOnlySet<Guid> branchIds,
        string ownerDisplayName,
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

public sealed record InstallOperationResult<T>(
    InstallOperationStatus Status,
    T? Value,
    string? Error,
    Guid? OrganizationId = null,
    Guid? BranchId = null,
    Guid? StaffUserId = null)
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

    public static InstallOperationResult<T> Conflict(string error, Guid organizationId, Guid branchId) =>
        new(InstallOperationStatus.Conflict, default, error, organizationId, branchId);
}

public enum InstallOperationStatus
{
    Succeeded,
    BadRequest,
    NotFound,
    Conflict
}
