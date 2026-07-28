using System.Reflection;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Platform.Auth;

namespace AFK4.Shared.Contracts.Tests.Identity;

public sealed class OrganizationPermissionNamesTests
{
    [Fact]
    public void EveryOrganizationPermissionUsesTheOrganizationNamespaceExactlyOnce()
    {
        var values = PublicStringConstants(typeof(OrganizationPermissionNames));

        Assert.NotEmpty(values);
        Assert.All(values, value => Assert.StartsWith("organization.", value));
        Assert.DoesNotContain(values, value => value.StartsWith("organization.organization.", StringComparison.Ordinal));
        Assert.Equal(values.Length, values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void PlatformRolesAndOrganizationPermissionsUseCanonicalValues()
    {
        Assert.Equal("platform_admin", PlatformAdminRoleNames.PlatformAdmin);
        Assert.Equal("platform_support", PlatformAdminRoleNames.PlatformSupport);
        Assert.Equal("organization.devices.install", OrganizationPermissionNames.InstallDevice);
        Assert.Equal("organization.billing.view", OrganizationPermissionNames.ViewBilling);
        Assert.Equal("organization.pos.sales.create", OrganizationPermissionNames.CreatePosSale);
    }

    private static string[] PublicStringConstants(Type type)
    {
        return type.GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => Assert.IsType<string>(field.GetRawConstantValue()))
            .ToArray();
    }
}
