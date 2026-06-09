using System.IO;
using AFK4.Player.Shell.Web;

namespace AFK4.Player.Shell.Tests.Web;

public sealed class PlayerWebAssetResolverTests
{
    [Fact]
    public void VirtualHost_IsPlayerLocalDomain()
    {
        Assert.Equal("player.afk4.local", PlayerWebAssetResolver.LocalVirtualHost);
    }

    [Fact]
    public void DevServerUrl_WhenLoopbackHttp_IsAccepted()
    {
        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: "http://127.0.0.1:5175",
            distIndexHtmlPath: null);

        Assert.Equal(PlayerWebLaunchKind.DevServer, target.Kind);
        Assert.Equal("http://127.0.0.1:5175", target.Source);
    }

    [Fact]
    public void DevServerUrl_WhenNotLoopback_IsRejected()
    {
        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: "http://example.com",
            distIndexHtmlPath: "/repo/src/AFK4.Player.Shell.Web/dist/index.html");

        Assert.Equal(PlayerWebLaunchKind.LocalFolder, target.Kind);
    }

    [Fact]
    public void NoDevServer_UsesDistFolderViaVirtualHost()
    {
        // Build the path with the OS-native separator so the expected folder
        // matches Path.GetDirectoryName on both Windows (\) and Linux (/).
        var indexPath = Path.Combine("repo", "src", "AFK4.Player.Shell.Web", "dist", "index.html");

        var target = PlayerWebAssetResolver.Resolve(
            devServerUrl: null,
            distIndexHtmlPath: indexPath);

        Assert.Equal(PlayerWebLaunchKind.LocalFolder, target.Kind);
        Assert.Equal(Path.GetDirectoryName(indexPath), target.LocalFolderPath);
        Assert.Equal("https://player.afk4.local/index.html", target.Source);
    }
}
