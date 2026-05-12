using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public static class PermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [StaffRoleNames.Owner] = new HashSet<string>
            {
                StaffPermissionNames.CreateDeviceEnrollmentCode,
                StaffPermissionNames.DispatchDeviceCommand,
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.RotateDeviceCredential,
                StaffPermissionNames.RevokeDeviceCredential,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.ManageRoles,
                StaffPermissionNames.ViewAudit
            },
            [StaffRoleNames.BranchManager] = new HashSet<string>
            {
                StaffPermissionNames.CreateDeviceEnrollmentCode,
                StaffPermissionNames.DispatchDeviceCommand,
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.RotateDeviceCredential,
                StaffPermissionNames.RevokeDeviceCredential,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.ViewAudit
            },
            [StaffRoleNames.ShiftSupervisor] = new HashSet<string>
            {
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.ViewAudit
            },
            [StaffRoleNames.CashierOperator] = new HashSet<string>
            {
                StaffPermissionNames.ViewFloorMap
            },
            [StaffRoleNames.Technician] = new HashSet<string>
            {
                StaffPermissionNames.CreateDeviceEnrollmentCode,
                StaffPermissionNames.DispatchDeviceCommand,
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.RotateDeviceCredential,
                StaffPermissionNames.RevokeDeviceCredential,
                StaffPermissionNames.ViewFloorMap
            },
            [StaffRoleNames.AccountantAuditor] = new HashSet<string>
            {
                StaffPermissionNames.ViewAudit
            }
        };

    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
        {
            if (!RolePermissions.TryGetValue(roleName, out var rolePermissions))
            {
                continue;
            }

            permissions.UnionWith(rolePermissions);
        }

        return permissions;
    }
}
