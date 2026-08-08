using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Проверки лимитов тарифа в точках роста. Возвращают <c>null</c>, если добавлять можно,
/// и <see cref="PlanLimitExceededDto"/> с числами, если нельзя.
/// </summary>
public interface IPlanLimitGuard
{
    Task<PlanLimitExceededDto?> CheckBranchAsync(Guid organizationId, CancellationToken cancellationToken);

    Task<PlanLimitExceededDto?> CheckDeviceAsync(Guid organizationId, Guid branchId, CancellationToken cancellationToken);

    Task<PlanLimitExceededDto?> CheckConcurrentSessionAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <param name="excludingInviteId">
    /// Приглашение, которое не считать «непринятым» — при приёме именно оно превращается в
    /// сотрудника, а не добавляет место сверх уже занятого.
    /// </param>
    Task<PlanLimitExceededDto?> CheckStaffUserAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken, Guid? excludingInviteId = null);
}
