using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class PermissionCatalogInstallDeviceTests
{
    [Theory]
    [InlineData(StaffRoleNames.Owner)]
    [InlineData(StaffRoleNames.BranchManager)]
    [InlineData(StaffRoleNames.Technician)]
    public void InstallDevice_GrantedTo_InstallerRoles(string role)
    {
        var permissions = PermissionCatalog.GetPermissions([role]);
        Assert.Contains(StaffPermissionNames.InstallDevice, permissions);
    }

    [Theory]
    [InlineData(StaffRoleNames.CashierOperator)]
    [InlineData(StaffRoleNames.ShiftSupervisor)]
    [InlineData(StaffRoleNames.AccountantAuditor)]
    public void InstallDevice_NotGrantedTo_OtherRoles(string role)
    {
        var permissions = PermissionCatalog.GetPermissions([role]);
        Assert.DoesNotContain(StaffPermissionNames.InstallDevice, permissions);
    }
}
