using AFK4.OrganizationAdmin.Web;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminDataFolderTests
{
    [Fact]
    public void Resolve_UsesPerUserLocalAppDataFolder()
    {
        var localAppData = Path.Combine("C:\\", "Users", "cashier", "AppData", "Local");

        var folder = OrganizationAdminDataFolder.Resolve(specialFolder =>
            specialFolder == Environment.SpecialFolder.LocalApplicationData
                ? localAppData
                : throw new InvalidOperationException($"Unexpected special folder {specialFolder}."));

        Assert.Equal(Path.Combine(localAppData, "AFK4", "OrganizationAdmin", "Miniblink"), folder);
        Assert.DoesNotContain("Program Files", folder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Resolve_FailsWhenLocalAppDataIsMissing()
    {
        Assert.Throws<InvalidOperationException>(() => OrganizationAdminDataFolder.Resolve(_ => string.Empty));
    }
}
