using AFK4.Platform.Api.Identity;

namespace AFK4.Platform.Api.Tests.Identity;

public sealed class OrganizationPermissionCatalogTests
{
    [Theory]
    [InlineData("organization_owner")]
    [InlineData("branch_manager")]
    [InlineData("shift_supervisor")]
    [InlineData("operator")]
    [InlineData("technician")]
    [InlineData("accountant")]
    public void CanonicalOrganizationRolesAreKnown(string roleName)
    {
        Assert.True(OrganizationPermissionCatalog.IsKnownRole(roleName));
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("cashier_operator")]
    [InlineData("accountant_auditor")]
    public void LegacyOrganizationRolesAreRejected(string roleName)
    {
        Assert.False(OrganizationPermissionCatalog.IsKnownRole(roleName));
    }
}
