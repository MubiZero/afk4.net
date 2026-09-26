namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// Картинки для экрана игрока — обложки игр и витрина. Агент кладёт их в общую папку ПК,
/// хост отдаёт её странице отдельным виртуальным хостом, только на чтение: без сети экран
/// показывает то, что уже скачано.
/// </summary>
public static class ShellShowcaseAssets
{
    public const string VirtualHost = "showcase.afk4.local";

    public const string CoversFolder = "covers";

    public const string CardsFolder = "cards";

    public static string Directory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "AFK4", "Showcase");
}
