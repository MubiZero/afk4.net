using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

public static class PlatformAdminPermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [PlatformAdminRoleNames.PlatformOwner] = new HashSet<string>
            {
                PlatformAdminPermissionNames.ViewTenants,
                PlatformAdminPermissionNames.CreateTenant,
                PlatformAdminPermissionNames.UpdateTenantStatus,
                PlatformAdminPermissionNames.UpdateTenantPlan,
                PlatformAdminPermissionNames.UpdateTenantLimits,
                PlatformAdminPermissionNames.ViewTenantSupportNotes,
                PlatformAdminPermissionNames.ManageTenantSupportNotes,
                PlatformAdminPermissionNames.ManageOwnerInvites,
                PlatformAdminPermissionNames.ViewTenantHealth,
                PlatformAdminPermissionNames.ViewPlatformAudit
            },
            [PlatformAdminRoleNames.PlatformSupport] = new HashSet<string>
            {
                PlatformAdminPermissionNames.ViewTenants,
                PlatformAdminPermissionNames.UpdateTenantStatus,
                PlatformAdminPermissionNames.ViewTenantSupportNotes,
                PlatformAdminPermissionNames.ManageTenantSupportNotes,
                PlatformAdminPermissionNames.ManageOwnerInvites,
                PlatformAdminPermissionNames.ViewTenantHealth,
                PlatformAdminPermissionNames.ViewPlatformAudit
            }
        };

    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
        {
            if (RolePermissions.TryGetValue(roleName, out var rolePermissions))
            {
                permissions.UnionWith(rolePermissions);
            }
        }

        return permissions;
    }

    public static bool IsKnownRole(string roleName)
    {
        return RolePermissions.ContainsKey(roleName);
    }
}
