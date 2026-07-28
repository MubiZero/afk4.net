using AFK4.OrganizationAdmin.Web;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminWebView2UserDataFolderTests
{
    [Fact]
    public void Resolve_UsesPerUserLocalAppDataFolder()
    {
        var localAppData = Path.Combine("C:\\", "Users", "cashier", "AppData", "Local");

        var folder = OrganizationAdminWebView2UserDataFolder.Resolve(specialFolder =>
            specialFolder == Environment.SpecialFolder.LocalApplicationData
                ? localAppData
                : throw new InvalidOperationException($"Unexpected special folder {specialFolder}."));

        Assert.Equal(Path.Combine(localAppData, "AFK4", "OrganizationAdmin", "WebView2"), folder);
        Assert.DoesNotContain("Program Files", folder, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("AFK4.OrganizationAdmin.App.exe.WebView2", folder, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EnsureExists_CreatesResolvedFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afk4-webview2-{Guid.NewGuid():N}");

        try
        {
            var folder = OrganizationAdminWebView2UserDataFolder.EnsureExists(_ => root);

            Assert.Equal(Path.Combine(root, "AFK4", "OrganizationAdmin", "WebView2"), folder);
            Assert.True(Directory.Exists(folder));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
