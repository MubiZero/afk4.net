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

        if (!staffContext.Permissions.Contains(permission))
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
