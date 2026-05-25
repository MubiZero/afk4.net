using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Install;

public interface IInstallService
{
    Task<InstallOperationResult<InstallDiscoverResponse>> DiscoverAsync(
        InstallDiscoverRequest request,
        CancellationToken cancellationToken);

    Task<InstallOperationResult<InstallEnrollResponse>> EnrollAsync(
        InstallEnrollRequest request,
        CancellationToken cancellationToken);
}

public sealed record InstallOperationResult<T>(
    InstallOperationStatus Status,
    T? Value,
    string? Error,
    Guid? OrganizationId = null,
    Guid? BranchId = null,
    Guid? OwnerCodeId = null)
{
    public bool Succeeded => Status == InstallOperationStatus.Succeeded;

    public static InstallOperationResult<T> Success(
        T value,
        Guid organizationId,
        Guid? branchId,
        Guid ownerCodeId) =>
        new(InstallOperationStatus.Succeeded, value, null, organizationId, branchId, ownerCodeId);

    public static InstallOperationResult<T> BadRequest(string error, Guid? ownerCodeId = null) =>
        new(InstallOperationStatus.BadRequest, default, error, OwnerCodeId: ownerCodeId);

    public static InstallOperationResult<T> NotFound(string error, Guid? ownerCodeId = null) =>
        new(InstallOperationStatus.NotFound, default, error, OwnerCodeId: ownerCodeId);

    public static InstallOperationResult<T> Conflict(string error, Guid organizationId, Guid branchId, Guid ownerCodeId) =>
        new(InstallOperationStatus.Conflict, default, error, organizationId, branchId, ownerCodeId);
}

public enum InstallOperationStatus
{
    Succeeded,
    BadRequest,
    NotFound,
    Conflict
}
