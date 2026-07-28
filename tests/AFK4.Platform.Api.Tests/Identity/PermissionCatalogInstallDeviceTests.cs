using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class OrganizationPermissionCatalogInstallDeviceTests
{
    [Theory]
    [InlineData(OrganizationRoleNames.OrganizationOwner)]
    [InlineData(OrganizationRoleNames.BranchManager)]
    [InlineData(OrganizationRoleNames.Technician)]
    public void InstallDevice_GrantedTo_InstallerRoles(string role)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([role]);
        Assert.Contains(OrganizationPermissionNames.InstallDevice, permissions);
    }

    [Theory]
    [InlineData(OrganizationRoleNames.Operator)]
    [InlineData(OrganizationRoleNames.ShiftSupervisor)]
    [InlineData(OrganizationRoleNames.Accountant)]
    public void InstallDevice_NotGrantedTo_OtherRoles(string role)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([role]);
        Assert.DoesNotContain(OrganizationPermissionNames.InstallDevice, permissions);
    }
}
