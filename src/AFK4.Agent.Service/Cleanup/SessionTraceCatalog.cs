using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Cleanup;

public enum TraceTargetKind
{
    /// <summary>Папка целиком.</summary>
    Directory,

    /// <summary>Один файл.</summary>
    File,

    /// <summary>Файлы папки по маске — только в ней, без вложенных.</summary>
    FilesMatching,

    /// <summary>Из <c>config.vdf</c> Steam вырезается блок <c>ConnectCache</c>: остальной файл — настройки клиента.</summary>
    SteamConnectCache
}

/// <param name="Pattern">Маска для <see cref="TraceTargetKind.FilesMatching"/>.</param>
public sealed record TraceTarget(string Item, TraceTargetKind Kind, string Path, string? Pattern = null);

/// <summary>Значение в кусте игрока (<c>HKU\SID\…</c>): удалить или записать DWORD.</summary>
public sealed record UserRegistryTrace(string Item, string SubKey, string ValueName, int? SetDword = null);

/// <summary>
/// Что именно стирается после сессии (спека оболочки, §6.4). Пути точные и живут здесь, а не в
/// профиле: Панель выбирает пункты, но не пути. Сохранения игр — «Документы», «Сохранённые игры»,
/// папки самих игр — сюда не входят ни одним путём.
/// </summary>
public static class SessionTraceCatalog
{
    private static readonly string[] SteamProcesses = ["steam.exe", "steamwebhelper.exe"];

    private static readonly string[] BrowserProcesses =
        ["chrome.exe", "msedge.exe", "firefox.exe", "opera.exe", "browser.exe", "brave.exe"];

    private static readonly string[] LauncherProcesses =
    [
        "EpicGamesLauncher.exe", "EpicWebHelper.exe",
        "Battle.net.exe",
        "RiotClientServices.exe", "RiotClientUx.exe", "RiotClientUxRender.exe",
        "upc.exe", "UplayWebCore.exe"
    ];

    private static readonly string[] MessengerProcesses = ["Discord.exe", "Telegram.exe"];

    /// <summary>
    /// Программы, которые надо закрыть до стирания, где бы и когда бы они ни стартовали: запущенный
    /// с утра Steam держит <c>loginusers.vdf</c> и перепишет его при выходе, а вход в нём — уже
    /// следующего игрока.
    /// </summary>
    public static IReadOnlySet<string> ProcessesToClose(IEnumerable<string> items)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in items)
        {
            names.UnionWith(item switch
            {
                SessionTraceNames.Steam => SteamProcesses,
                SessionTraceNames.Browsers => BrowserProcesses,
                SessionTraceNames.Launchers => LauncherProcesses,
                SessionTraceNames.Messengers => MessengerProcesses,
                _ => []
            });
        }

        return names;
    }

    /// <param name="profileDirectory">Профиль игрока: <c>C:\Users\AFK4 Player</c>.</param>
    /// <param name="steamDirectory">Папка Steam; null — Steam на ПК не стоит.</param>
    public static IReadOnlyList<TraceTarget> Targets(
        IEnumerable<string> items,
        string profileDirectory,
        string? steamDirectory)
    {
        var local = Path.Combine(profileDirectory, "AppData", "Local");
        var roaming = Path.Combine(profileDirectory, "AppData", "Roaming");
        var targets = new List<TraceTarget>();
        foreach (var item in items.Distinct())
        {
            switch (item)
            {
                case SessionTraceNames.Steam:
                    if (steamDirectory is not null)
                    {
                        targets.Add(new(item, TraceTargetKind.File, Path.Combine(steamDirectory, "config", "loginusers.vdf")));
                        targets.Add(new(item, TraceTargetKind.SteamConnectCache, Path.Combine(steamDirectory, "config", "config.vdf")));
                        // Файлы Steam Guard старых версий клиента: по ним вход проходил без кода.
                        targets.Add(new(item, TraceTargetKind.FilesMatching, steamDirectory, "ssfn*"));
                    }

                    // Куки магазина и сообщества во встроенном браузере Steam.
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Steam", "htmlcache")));
                    break;

                case SessionTraceNames.Browsers:
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Google", "Chrome", "User Data")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Microsoft", "Edge", "User Data")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Yandex", "YandexBrowser", "User Data")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "BraveSoftware", "Brave-Browser", "User Data")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(roaming, "Mozilla", "Firefox", "Profiles")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Mozilla", "Firefox", "Profiles")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(roaming, "Opera Software", "Opera Stable")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(roaming, "Opera Software", "Opera GX Stable")));
                    break;

                case SessionTraceNames.Launchers:
                    // Epic: «запомнить меня» — секция RememberMe в этом файле.
                    targets.Add(new(item, TraceTargetKind.File, Path.Combine(local, "EpicGamesLauncher", "Saved", "Config", "Windows", "GameUserSettings.ini")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "EpicGamesLauncher", "Saved", "webcache")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "EpicGamesLauncher", "Saved", "webcache_4147")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "EpicGamesLauncher", "Saved", "webcache_4430")));
                    // Battle.net: список запомненных аккаунтов и куки входа.
                    targets.Add(new(item, TraceTargetKind.File, Path.Combine(roaming, "Battle.net", "Battle.net.config")));
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(local, "Battle.net", "BrowserCaches")));
                    // Riot: «не выходить» живёт в этом файле.
                    targets.Add(new(item, TraceTargetKind.File, Path.Combine(local, "Riot Games", "Riot Client", "Data", "RiotGamesPrivateSettings.yaml")));
                    // Ubisoft Connect: сохранённый вход.
                    targets.Add(new(item, TraceTargetKind.File, Path.Combine(local, "Ubisoft Game Launcher", "ConnectSecureStorage.dat")));
                    break;

                case SessionTraceNames.Messengers:
                    // Discord пересоздаёт папку сам; программа лежит в Local\Discord и не тронута.
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(roaming, "discord")));
                    // Telegram Desktop: вход и переписка — tdata; сама программа рядом, её не трогаем.
                    targets.Add(new(item, TraceTargetKind.Directory, Path.Combine(roaming, "Telegram Desktop", "tdata")));
                    break;
            }
        }

        return targets;
    }

    /// <summary>Автовход Steam хранится в кусте игрока, а не в файлах.</summary>
    public static IReadOnlyList<UserRegistryTrace> RegistryTraces(IEnumerable<string> items) =>
        items.Contains(SessionTraceNames.Steam)
            ?
            [
                new(SessionTraceNames.Steam, @"Software\Valve\Steam", "AutoLoginUser"),
                new(SessionTraceNames.Steam, @"Software\Valve\Steam", "RememberPassword", SetDword: 0)
            ]
            : [];

    /// <summary>
    /// Путь лежит внутри корня. Последний сторож перед удалением: что бы ни пришло в каталог, агент не
    /// сотрёт ничего вне профиля игрока и папки Steam.
    /// </summary>
    public static bool IsInside(string path, string root)
    {
        if (string.IsNullOrWhiteSpace(root))
        {
            return false;
        }

        var fullRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        var fullPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
        // Корень диска — не профиль: «C:\» внутри себя содержит всё.
        if (Path.GetPathRoot(fullRoot) is { } driveRoot &&
            string.Equals(Path.TrimEndingDirectorySeparator(driveRoot), fullRoot, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return fullPath.Length > fullRoot.Length
            && fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
            && (fullPath[fullRoot.Length] == Path.DirectorySeparatorChar || fullPath[fullRoot.Length] == Path.AltDirectorySeparatorChar);
    }
}
