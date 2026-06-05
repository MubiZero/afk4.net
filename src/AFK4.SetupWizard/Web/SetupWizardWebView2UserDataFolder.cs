using System.IO;

namespace AFK4.SetupWizard.Web;

public static class SetupWizardWebView2UserDataFolder
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

        return Path.Combine(localAppData, "AFK4", "SetupWizard", "WebView2");
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
