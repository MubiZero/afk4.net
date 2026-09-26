using AFK4.Shared.Contracts.Platform.Organizations;

namespace AFK4.Platform.Api.Platform.Entitlements;

/// <summary>
/// Проверки лимитов тарифа в точках роста. Возвращают <c>null</c>, если добавлять можно,
/// и <see cref="PlanLimitExceededDto"/> с числами, если нельзя.
/// </summary>
public interface IPlanLimitGuard
{
    Task<PlanLimitExceededDto?> CheckBranchAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// Лимит ПК филиала. Считаются только игровые ПК: рабочее место управляющего играм не служит и
    /// место в лимите бесплатного тарифа («до десяти ПК») не занимает — его регистрация не упирается.
    /// </summary>
    Task<PlanLimitExceededDto?> CheckDeviceAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken, string role = AFK4.Shared.Contracts.Install.DeviceRoleNames.GamingPc);

    Task<PlanLimitExceededDto?> CheckConcurrentSessionAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>
    /// ПК «вне тарифа» — сверх предела ПК на клуб (спека тарифов клуба, §5a): новую сессию на нём не
    /// начать и чужую на него не перенести. Идущая сессия доживает — её эта проверка не трогает.
    /// </summary>
    Task<PlanLimitExceededDto?> CheckDeviceOnPlanAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken);

    /// <param name="excludingInviteId">
    /// Приглашение, которое не считать «непринятым» — при приёме именно оно превращается в
    /// сотрудника, а не добавляет место сверх уже занятого.
    /// </param>
    Task<PlanLimitExceededDto?> CheckStaffUserAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken, Guid? excludingInviteId = null);
}
