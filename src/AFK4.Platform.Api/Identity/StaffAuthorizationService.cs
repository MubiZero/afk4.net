using AFK4.Platform.Api.Tenancy;

namespace AFK4.Platform.Api.Identity;

public sealed class StaffAuthorizationService(
    IStaffContextAccessor staffContextAccessor,
    IBranchResolver branchResolver)
{
    public async Task<StaffAuthorizationResult> RequireBranchPermissionAsync(
        Guid branchId,
        string permission,
        CancellationToken cancellationToken)
    {
        var staffContext = staffContextAccessor.Current;

        if (staffContext is null)
        {
            return StaffAuthorizationResult.Unauthenticated();
        }

        var branch = await branchResolver.FindAsync(branchId, cancellationToken);

        if (branch is null || branch.OrganizationId != staffContext.OrganizationId)
        {
            return StaffAuthorizationResult.Denied(staffContext, "Branch is not available to this organization.");
        }

        if (!staffContext.BranchIds.Contains(branchId))
        {
            return StaffAuthorizationResult.Denied(staffContext, "Staff user is not assigned to this branch.");
        }

        if (!staffContext.HasBranchPermission(branchId, permission))
        {
            return StaffAuthorizationResult.Denied(staffContext, "Staff user does not have the required permission.");
        }

        return StaffAuthorizationResult.Allowed(staffContext);
    }

    /// <summary>
    /// Пускает, если есть хоть одно право из списка. Нужно там, где один экран собирает несколько
    /// разделов сразу: поиск по филиалу кассиру возвращает чеки, оператору — места, и требовать от
    /// обоих одно и то же право значило бы закрыть экран половине смены.
    /// </summary>
    public async Task<StaffAuthorizationResult> RequireBranchAnyPermissionAsync(
        Guid branchId,
        IReadOnlyCollection<string> permissions,
        CancellationToken cancellationToken)
    {
        var staffContext = staffContextAccessor.Current;

        if (staffContext is null)
        {
            return StaffAuthorizationResult.Unauthenticated();
        }

        var branch = await branchResolver.FindAsync(branchId, cancellationToken);

        if (branch is null || branch.OrganizationId != staffContext.OrganizationId)
        {
            return StaffAuthorizationResult.Denied(staffContext, "Branch is not available to this organization.");
        }

        if (!staffContext.BranchIds.Contains(branchId))
        {
            return StaffAuthorizationResult.Denied(staffContext, "Staff user is not assigned to this branch.");
        }

        if (!permissions.Any(permission => staffContext.HasBranchPermission(branchId, permission)))
        {
            return StaffAuthorizationResult.Denied(staffContext, "Staff user does not have the required permission.");
        }

        return StaffAuthorizationResult.Allowed(staffContext);
    }

    public StaffAuthorizationResult RequireOrganizationPermission(string permission)
    {
        var staffContext = staffContextAccessor.Current;

        if (staffContext is null)
        {
            return StaffAuthorizationResult.Unauthenticated();
        }

        if (!staffContext.Permissions.Contains(permission))
        {
            return StaffAuthorizationResult.Denied(staffContext, "Staff user does not have the required permission.");
        }

        return StaffAuthorizationResult.Allowed(staffContext);
    }
}
