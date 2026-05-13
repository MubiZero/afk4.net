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
                StaffPermissionNames.ViewDeviceDetail,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.StartSession,
                StaffPermissionNames.ExtendSession,
                StaffPermissionNames.TransferSession,
                StaffPermissionNames.EndSession,
                StaffPermissionNames.ViewSession,
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
                StaffPermissionNames.ViewDeviceDetail,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.StartSession,
                StaffPermissionNames.ExtendSession,
                StaffPermissionNames.TransferSession,
                StaffPermissionNames.EndSession,
                StaffPermissionNames.ViewSession,
                StaffPermissionNames.ViewAudit
            },
            [StaffRoleNames.ShiftSupervisor] = new HashSet<string>
            {
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.ViewDeviceDetail,
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.StartSession,
                StaffPermissionNames.ExtendSession,
                StaffPermissionNames.TransferSession,
                StaffPermissionNames.EndSession,
                StaffPermissionNames.ViewSession,
                StaffPermissionNames.ViewAudit
            },
            [StaffRoleNames.CashierOperator] = new HashSet<string>
            {
                StaffPermissionNames.ViewFloorMap,
                StaffPermissionNames.StartSession,
                StaffPermissionNames.ExtendSession,
                StaffPermissionNames.TransferSession,
                StaffPermissionNames.EndSession,
                StaffPermissionNames.ViewSession
            },
            [StaffRoleNames.Technician] = new HashSet<string>
            {
                StaffPermissionNames.CreateDeviceEnrollmentCode,
                StaffPermissionNames.DispatchDeviceCommand,
                StaffPermissionNames.ViewDeviceCommandStatus,
                StaffPermissionNames.RotateDeviceCredential,
                StaffPermissionNames.RevokeDeviceCredential,
                StaffPermissionNames.ViewDeviceDetail,
                StaffPermissionNames.ViewFloorMap
            },
            [StaffRoleNames.AccountantAuditor] = new HashSet<string>
            {
                StaffPermissionNames.ViewSession,
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
