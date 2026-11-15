namespace AFK4.OrganizationAdmin.Web;

/// <summary>Папка данных движка Панели (cookies, localStorage) — в профиле оператора, а не рядом с exe: туда писать нельзя.</summary>
public static class OrganizationAdminDataFolder
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

        return Path.Combine(localAppData, "AFK4", "OrganizationAdmin", "Miniblink");
    }
}
