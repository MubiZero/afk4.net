using AFK4.Platform.Api.Identity;
using AFK4.Shared.Contracts.Identity;
using Xunit;

namespace AFK4.Platform.Api.Tests.Identity;

// Guards the Этап-0 §2 visibility contract on the backend side: the shift_supervisor
// reconciliation (approve money actions; no audit/diagnostics → no Управление).
// Mirror of the frontend operatorVisibility.test.ts fixture.
public sealed class OrganizationPermissionCatalogContractTests
{
    [Fact]
    public void ShiftSupervisor_CanApproveMoneyActions()
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.ShiftSupervisor]);
        Assert.Contains(OrganizationPermissionNames.ApproveMoneyAction, permissions);
    }

    [Theory]
    [InlineData(OrganizationPermissionNames.ViewAudit)]
    [InlineData(OrganizationPermissionNames.ViewDiagnostics)]
    public void ShiftSupervisor_HasNoManagementOnlyVisibility(string permission)
    {
        var permissions = OrganizationPermissionCatalog.GetPermissions([OrganizationRoleNames.ShiftSupervisor]);
        Assert.DoesNotContain(permission, permissions);
    }
}
