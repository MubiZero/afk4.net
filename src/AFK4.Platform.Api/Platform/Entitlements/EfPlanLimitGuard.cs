using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Platform.Organizations;
using AFK4.Shared.Contracts.Sessions;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Entitlements;

public sealed class EfPlanLimitGuard(PlatformDbContext dbContext, TimeProvider? timeProvider = null) : IPlanLimitGuard
{
    // Снятое и отклонённое устройство места на филиале не занимает; ожидающее одобрения — занимает,
    // иначе очередь из ожидающих перепрыгнет лимит в момент одобрения.
    private static readonly string[] LiveDeviceStates =
        [DeviceEnrollmentStateNames.Approved, DeviceEnrollmentStateNames.Pending];

    // «Одновременный» сеанс — любой, который ещё не закрыт.
    private static readonly string[] LiveSessionStates =
        [SessionStateNames.Requested, SessionStateNames.Active, SessionStateNames.Paused, SessionStateNames.Ending];

    public async Task<PlanLimitExceededDto?> CheckBranchAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxBranches is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Branches
            .CountAsync(branch => branch.OrganizationId == organizationId, cancellationToken);

        return Verdict(PlanLimitNames.Branches, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckDeviceAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken, string role = DeviceRoleNames.GamingPc)
    {
        if (role != DeviceRoleNames.GamingPc)
        {
            return null;
        }

        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan is null)
        {
            return null;
        }

        if (plan.Limits.MaxDevices is { } organizationLimit)
        {
            var inOrganization = await dbContext.Devices
                .CountAsync(
                    device => device.OrganizationId == organizationId
                        && device.Role == DeviceRoleNames.GamingPc
                        && LiveDeviceStates.Contains(device.EnrollmentState),
                    cancellationToken);
            if (Verdict(PlanLimitNames.Devices, organizationLimit, inOrganization, plan.PlanCode) is { } refused)
            {
                return refused;
            }
        }

        if (plan.Limits.MaxDevicesPerBranch is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Devices
            .CountAsync(
                device => device.OrganizationId == organizationId
                    && device.BranchId == branchId
                    && device.Role == DeviceRoleNames.GamingPc
                    && LiveDeviceStates.Contains(device.EnrollmentState),
                cancellationToken);

        return Verdict(PlanLimitNames.DevicesPerBranch, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckDeviceOnPlanAsync(Guid organizationId, Guid deviceId, CancellationToken cancellationToken)
    {
        var allowance = await PlanDevices.ForOrganizationAsync(dbContext, organizationId, cancellationToken);
        if (allowance.Limit is not { } limit || !allowance.Outside.Contains(deviceId))
        {
            return null;
        }

        var planCode = await dbContext.Organizations.AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => organization.PlanCode)
            .SingleAsync(cancellationToken);
        return new PlanLimitExceededDto(PlanLimitNames.ReachedCode, PlanLimitNames.Devices, limit, allowance.Count, planCode);
    }

    public async Task<PlanLimitExceededDto?> CheckConcurrentSessionAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxConcurrentSessions is not { } limit)
        {
            return null;
        }

        var current = await dbContext.Sessions
            .CountAsync(
                session => session.OrganizationId == organizationId
                    && LiveSessionStates.Contains(session.State),
                cancellationToken);

        return Verdict(PlanLimitNames.ConcurrentSessions, limit, current, plan.PlanCode);
    }

    public async Task<PlanLimitExceededDto?> CheckStaffUserAsync(
        Guid organizationId, Guid branchId, CancellationToken cancellationToken, Guid? excludingInviteId = null)
    {
        var plan = await LoadPlanAsync(organizationId, cancellationToken);
        if (plan?.Limits.MaxStaffUsersPerBranch is not { } limit)
        {
            return null;
        }

        var activeUsers = await dbContext.StaffRoleAssignments
            .Where(assignment => assignment.OrganizationId == organizationId && assignment.BranchId == branchId)
            .Where(assignment => dbContext.StaffUsers
                .Any(user => user.StaffUserId == assignment.StaffUserId && user.IsActive))
            .Select(assignment => assignment.StaffUserId)
            .Distinct()
            .CountAsync(cancellationToken);

        // Непринятое живое приглашение занимает место заранее: иначе три приглашения на филиал
        // с лимитом два пройдут проверку по очереди и перепрыгнут границу в момент приёма.
        // Само принимаемое приглашение (excludingInviteId) — исключение: приём не добавляет
        // место сверх занятого, он превращает это же приглашение в сотрудника. Истёкший или
        // исчерпавший попытки код в сотрудника уже не превратится — места он не держит.
        var now = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var pendingInvites = await dbContext.StaffInvites
            .CountAsync(
                invite => invite.OrganizationId == organizationId
                    && invite.BranchId == branchId
                    && invite.AcceptedAtUtc == null
                    && invite.ExpiresAtUtc > now
                    && invite.AttemptCount < EfStaffInviteService.MaxAttempts
                    && invite.StaffInviteId != excludingInviteId,
                cancellationToken);

        return Verdict(PlanLimitNames.StaffUsersPerBranch, limit, activeUsers + pendingInvites, plan.PlanCode);
    }

    private async Task<PlanSnapshot?> LoadPlanAsync(Guid organizationId, CancellationToken cancellationToken)
    {
        var row = await dbContext.Organizations
            .AsNoTracking()
            .Where(organization => organization.OrganizationId == organizationId)
            .Select(organization => new { organization.LimitsJson, organization.PlanCode })
            .SingleOrDefaultAsync(cancellationToken);

        // Организации нет — отказывать по лимиту нельзя: за «нет такой» отвечает вызывающий код
        // своей ошибкой, иначе пользователь получит ложное объяснение отказа.
        return row is null ? null : new PlanSnapshot(OrganizationLimitsJson.Deserialize(row.LimitsJson), row.PlanCode);
    }

    // «Стоп на рост»: спрашиваем, станет ли больше разрешённого, а не превышено ли сейчас.
    // Клуб, уже находящийся выше лимита, продолжает работать — проверка зовётся только перед
    // добавлением нового.
    private static PlanLimitExceededDto? Verdict(string limitName, int limit, int current, string planCode) =>
        current >= limit
            ? new PlanLimitExceededDto(PlanLimitNames.ReachedCode, limitName, limit, current, planCode)
            : null;

    private sealed record PlanSnapshot(OrganizationLimitsDto Limits, string PlanCode);
}
