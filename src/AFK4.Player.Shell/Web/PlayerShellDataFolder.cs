namespace AFK4.Player.Shell.Web;

/// <summary>
/// Папка данных движка экрана игрока (cookies, localStorage) — в профиле игрока, а не рядом с exe: оболочка стоит в
/// Program Files, и писать туда обычному пользователю нельзя.
/// </summary>
public static class PlayerShellDataFolder
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

        return Path.Combine(localAppData, "AFK4", "Player", "Miniblink");
    }
}
