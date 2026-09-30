using AFK4.Agent.Service.Cleanup;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Games;

/// <summary>Запущенная игроком игра и её процессы на этом ПК — то, что видно в «Моих приложениях».</summary>
public sealed record LaunchedAppView(Guid LaunchId, string AppId, string DisplayName, IReadOnlyList<int> ProcessIds);

/// <summary>
/// Что игрок запустил из библиотеки в этой сессии. Агент помнит каждый запуск и по списку процессов
/// игрока узнаёт, что к нему относится: сам запущенный процесс, его потомки и то, что за него
/// запустил уже работавший лаунчер. Системные процессы, оболочка и агент сюда не попадают никогда.
///
/// Время запуска — по часам самой машины, а не платформы: сравнивается со временем старта процессов,
/// которое тоже читается с этой машины.
/// </summary>
public sealed class LaunchedApps(IPlayerSessionHost host, Func<DateTimeOffset>? localNow = null)
{
    /// <summary>Запись о запуске делается после создания процесса: его старт оказывается чуть раньше неё.</summary>
    private static readonly TimeSpan StartSlack = TimeSpan.FromSeconds(10);

    /// <summary>Запуск без единого живого процесса забывается не сразу: игра из Steam открывается минуту.</summary>
    private static readonly TimeSpan ForgetAfter = TimeSpan.FromMinutes(10);

    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(1);

    private static readonly IReadOnlySet<string> LauncherImages = SessionTraceCatalog.ProcessesToClose(SessionTraceNames.All);

    private sealed record Launch(Guid Id, string AppId, string DisplayName, int? RootProcessId, DateTimeOffset At);

    private readonly Func<DateTimeOffset> now = localNow ?? (() => DateTimeOffset.UtcNow);
    private readonly object gate = new();
    private readonly List<Launch> launches = [];
    private IReadOnlyList<LaunchedAppView>? cached;
    private DateTimeOffset cachedAt;

    public void Register(string appId, string displayName, int? rootProcessId)
    {
        lock (gate)
        {
            launches.Add(new Launch(Guid.NewGuid(), appId, displayName, rootProcessId, now()));
            cached = null;
        }
    }

    /// <summary>Что ещё работает. Без запусков процессы не перебираются вовсе.</summary>
    public IReadOnlyList<LaunchedAppView> Running()
    {
        lock (gate)
        {
            if (launches.Count == 0)
            {
                return [];
            }

            if (cached is not null && now() - cachedAt < CacheFor)
            {
                return cached;
            }

            return Refresh();
        }
    }

    /// <summary>То же по готовому списку процессов — для уборки, у которой он уже есть.</summary>
    public IReadOnlyList<LaunchedAppView> Attribute(IReadOnlyList<SessionProcess> processes)
    {
        lock (gate)
        {
            return AttributeLocked(processes);
        }
    }

    /// <summary>Закрыть игру со всеми её процессами. false — такого запуска нет.</summary>
    public bool Close(Guid launchId)
    {
        lock (gate)
        {
            if (!launches.Any(launch => launch.Id == launchId))
            {
                return false;
            }

            var app = Refresh().FirstOrDefault(view => view.LaunchId == launchId);
            foreach (var processId in app?.ProcessIds ?? [])
            {
                host.TryTerminate(processId);
            }

            launches.RemoveAll(launch => launch.Id == launchId);
            cached = null;
            return true;
        }
    }

    /// <summary>Сессия кончилась: запуски следующему игроку не принадлежат.</summary>
    public void Clear()
    {
        lock (gate)
        {
            launches.Clear();
            cached = null;
        }
    }

    private IReadOnlyList<LaunchedAppView> Refresh()
    {
        var user = host.IsSupported ? host.ConsoleUser() : null;
        var views = user is null ? [] : AttributeLocked(host.Processes(user));

        // Вышла давно и без следа — в списке её нет, и помнить незачем.
        var alive = views.Select(view => view.LaunchId).ToHashSet();
        var current = now();
        launches.RemoveAll(launch => !alive.Contains(launch.Id) && current - launch.At > ForgetAfter);

        cached = views;
        cachedAt = current;
        return views;
    }

    private IReadOnlyList<LaunchedAppView> AttributeLocked(IReadOnlyList<SessionProcess> processes)
    {
        if (launches.Count == 0)
        {
            return [];
        }

        var byId = processes.GroupBy(process => process.ProcessId).ToDictionary(group => group.Key, group => group.First());
        var owner = new Dictionary<int, Launch>();
        var protectedRoots = host.ProtectedRoots;

        // Сам запущенный процесс — его запустил игрок из списка клуба, поэтому защищённая папка
        // ему не помеха. Лаунчер процессом игры не считается: Steam общий для всех игр.
        foreach (var launch in launches)
        {
            if (launch.RootProcessId is { } root
                && byId.TryGetValue(root, out var process)
                && !IsLauncher(process)
                && StartedNoEarlierThan(process, launch.At - StartSlack)
                && !owner.ContainsKey(root))
            {
                owner[root] = launch;
            }
        }

        // Родитель раньше потомка: идём по времени старта, и потомок всегда видит родителя уже в списке.
        foreach (var process in processes.OrderBy(p => p.StartedAtUtc ?? DateTimeOffset.MaxValue))
        {
            if (owner.ContainsKey(process.ProcessId) || OwnerOf(process, byId, owner, protectedRoots) is not { } found)
            {
                continue;
            }

            owner[process.ProcessId] = found;
        }

        return launches
            .Select(launch => new LaunchedAppView(
                launch.Id,
                launch.AppId,
                launch.DisplayName,
                owner.Where(pair => pair.Value == launch).Select(pair => pair.Key).Order().ToList()))
            .Where(view => view.ProcessIds.Count > 0)
            .ToList();
    }

    private Launch? OwnerOf(
        SessionProcess process,
        Dictionary<int, SessionProcess> byId,
        Dictionary<int, Launch> owner,
        IReadOnlyList<string> protectedRoots)
    {
        // Чей exe не прочитался — того не трогаем; защищённые папки (Windows, AFK4) — тем более.
        if (process.ParentProcessId is not { } parentId
            || !byId.TryGetValue(parentId, out var parent)
            || string.IsNullOrWhiteSpace(process.ExecutablePath)
            || protectedRoots.Any(root => SessionTraceCatalog.IsInside(process.ExecutablePath, root)))
        {
            return null;
        }

        // Номер родителя мог достаться другому процессу: родитель не может быть моложе потомка.
        if (parent.StartedAtUtc is { } parentStarted && process.StartedAtUtc is { } started && parentStarted > started)
        {
            return null;
        }

        if (owner.TryGetValue(parentId, out var parentOwner))
        {
            return parentOwner;
        }

        // Потомок лаунчера, запущенный после запуска игры игроком, — часть этой игры; если игр
        // несколько, то последней из запущенных до него.
        if (IsLauncher(parent) && !IsLauncher(process) && process.StartedAtUtc is { } time)
        {
            return launches.LastOrDefault(launch => launch.At - StartSlack <= time);
        }

        return null;
    }

    private static bool IsLauncher(SessionProcess process) => LauncherImages.Contains(process.ImageName);

    private static bool StartedNoEarlierThan(SessionProcess process, DateTimeOffset moment) =>
        process.StartedAtUtc is not { } started || started >= moment;
}
