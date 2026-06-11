using System.IO;

namespace AFK4.Player.Shell.Web;

// WebView2's default user-data folder sits next to the executable. The shell installs to
// C:\Program Files\AFK4\Player Shell (read-only for the interactive user), so the default
// folder can't be created and EnsureCoreWebView2Async throws -> the kiosk window crashes on
// load and the agent supervisor relaunches it in a loop. Pin the folder under LocalAppData,
// mirroring the operator app (OperatorWebView2UserDataFolder).
public static class PlayerWebView2UserDataFolder
{
    public static string Resolve()
    {
        return Resolve(Environment.GetFolderPath);
    }

    public static string Resolve(Func<Environment.SpecialFolder, string> getFolderPath)
    {
        ArgumentNullException.ThrowIfNull(getFolderPath);

        var localAppData = getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Local application data folder is not available.");
        }

        return Path.Combine(localAppData, "AFK4", "Player", "WebView2");
    }

    public static string EnsureExists()
    {
        return EnsureExists(Environment.GetFolderPath);
    }

    public static string EnsureExists(Func<Environment.SpecialFolder, string> getFolderPath)
    {
        var folder = Resolve(getFolderPath);
        Directory.CreateDirectory(folder);
        return folder;
    }
}
