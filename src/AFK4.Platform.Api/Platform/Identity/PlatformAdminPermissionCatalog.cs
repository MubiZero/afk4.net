using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Platform.Identity;

public static class PlatformAdminPermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [PlatformAdminRoleNames.PlatformAdmin] = new HashSet<string>
            {
                PlatformAdminPermissionNames.UseSupportAccess,
                PlatformAdminPermissionNames.ViewOrganizations,
                PlatformAdminPermissionNames.CreateOrganization,
                PlatformAdminPermissionNames.UpdateOrganizationStatus,
                PlatformAdminPermissionNames.UpdateOrganizationLimits,
                PlatformAdminPermissionNames.UpdateOrganizationProfile,
                PlatformAdminPermissionNames.UpdateOrganizationUpdateChannel,
                PlatformAdminPermissionNames.ViewOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationOwnerInvites,
                PlatformAdminPermissionNames.TransferOrganizationOwner,
                PlatformAdminPermissionNames.ViewOrganizationHealth,
                PlatformAdminPermissionNames.ViewPlatformAudit,
                PlatformAdminPermissionNames.ViewBilling,
                PlatformAdminPermissionNames.ManagePlans,
                PlatformAdminPermissionNames.ManageSubscriptions,
                PlatformAdminPermissionNames.ManageInvoices,
                PlatformAdminPermissionNames.ViewUpdates,
                PlatformAdminPermissionNames.ManageUpdatePackages,
                PlatformAdminPermissionNames.ManageUpdateRollouts
            },
            [PlatformAdminRoleNames.PlatformSupport] = new HashSet<string>
            {
                PlatformAdminPermissionNames.UseSupportAccess,
                PlatformAdminPermissionNames.ViewOrganizations,
                PlatformAdminPermissionNames.UpdateOrganizationStatus,
                PlatformAdminPermissionNames.ViewOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationSupportNotes,
                PlatformAdminPermissionNames.ManageOrganizationOwnerInvites,
                PlatformAdminPermissionNames.ViewOrganizationHealth,
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
