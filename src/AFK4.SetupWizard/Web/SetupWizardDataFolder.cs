namespace AFK4.SetupWizard.Web;

/// <summary>Папка данных движка мастера (cookies, localStorage) — в профиле, а не рядом с exe: туда писать нельзя.</summary>
public static class SetupWizardDataFolder
{
    public static string Resolve() => Resolve(Environment.GetFolderPath);

    public static string Resolve(Func<Environment.SpecialFolder, string> getFolderPath)
    {
        ArgumentNullException.ThrowIfNull(getFolderPath);

        var localAppData = getFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(localAppData))
        {
            throw new InvalidOperationException("Local application data folder is not available.");
        }

        return Path.Combine(localAppData, "AFK4", "SetupWizard", "Miniblink");
    }
}
