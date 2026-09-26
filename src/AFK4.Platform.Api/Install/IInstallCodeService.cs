using AFK4.Shared.Contracts.Install;

namespace AFK4.Platform.Api.Install;

/// <summary>Коды установки филиала в Панели: выдать, показать действующие, отозвать.</summary>
public interface IInstallCodeService
{
    Task<InstallOperationResult<InstallCodeDto>> CreateAsync(
        Guid organizationId,
        Guid branchId,
        Guid staffUserId,
        CreateInstallCodeRequest request,
        CancellationToken cancellationToken);

    /// <summary>Действующие: не истекли и не исчерпаны. Самого кода в них нет.</summary>
    Task<IReadOnlyList<InstallCodeDto>> ListActiveAsync(
        Guid organizationId,
        Guid branchId,
        CancellationToken cancellationToken);

    Task<bool> RevokeAsync(
        Guid organizationId,
        Guid branchId,
        Guid installCodeId,
        CancellationToken cancellationToken);
}
