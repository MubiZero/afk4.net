using System.IO;

namespace AFK4.Player.Shell.Web;

public enum PlayerWebLaunchKind
{
    DevServer,
    LocalFolder
}

public sealed record PlayerWebLaunchTarget(
    PlayerWebLaunchKind Kind,
    string Source,
    string? LocalFolderPath);

public static class PlayerWebAssetResolver
{
    public const string LocalVirtualHost = "player.afk4.local";

    public static PlayerWebLaunchTarget Resolve(string? devServerUrl, string? distIndexHtmlPath)
    {
        if (IsLoopbackHttp(devServerUrl))
        {
            return new PlayerWebLaunchTarget(PlayerWebLaunchKind.DevServer, devServerUrl!, LocalFolderPath: null);
        }

        var folder = Path.GetDirectoryName(distIndexHtmlPath)!;
        return new PlayerWebLaunchTarget(
            PlayerWebLaunchKind.LocalFolder,
            $"https://{LocalVirtualHost}/index.html",
            folder);
    }

    private static bool IsLoopbackHttp(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var schemeOk = uri.Scheme is "http" or "https";
        return schemeOk && uri.IsLoopback;
    }
}
