using AFK4.Platform.Api.Platform.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class PlatformAdminPermissionCatalogTests
{
    [Fact]
    public void PlatformSupport_CanAuditButCannotOperateBillingOrUpdates()
    {
        var permissions = PlatformAdminPermissionCatalog.GetPermissions([PlatformAdminRoleNames.PlatformSupport]);

        Assert.Contains(PlatformAdminPermissionNames.ViewPlatformAudit, permissions);
        Assert.DoesNotContain(PlatformAdminPermissionNames.ViewBilling, permissions);
        Assert.DoesNotContain(PlatformAdminPermissionNames.ViewUpdates, permissions);
        Assert.DoesNotContain(PlatformAdminPermissionNames.UpdateOrganizationProfile, permissions);
    }

    [Fact]
    public void PlatformAdmin_CanUpdateOrganizationProfile()
    {
        var permissions = PlatformAdminPermissionCatalog.GetPermissions([PlatformAdminRoleNames.PlatformAdmin]);

        Assert.Contains(PlatformAdminPermissionNames.UpdateOrganizationProfile, permissions);
    }
}
