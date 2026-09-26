using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Cleanup;

/// <summary>Кто сидит в Windows за этим ПК: консольная сессия, SID и папка профиля.</summary>
public sealed record PlayerSessionUser(int SessionId, string Sid, string ProfileDirectory);

/// <summary>То, что уборке нужно от самой Windows. Вне Windows — <see cref="IsSupported"/> false.</summary>
public interface IPlayerSessionHost
{
    bool IsSupported { get; }

    /// <summary>Папки, программы из которых не закрываются никогда: Windows, AFK4, среда WebView2.</summary>
    IReadOnlyList<string> ProtectedRoots { get; }

    PlayerSessionUser? ConsoleUser();

    /// <summary>Процессы этого пользователя в его сессии — и только его.</summary>
    IReadOnlyList<SessionProcess> Processes(PlayerSessionUser user);

    bool TryTerminate(int processId);

    bool IsRunning(int processId);

    string? SteamDirectory(PlayerSessionUser user);

    /// <summary>Удалить значение в кусте игрока. true — оно было и удалено.</summary>
    bool DeleteUserValue(PlayerSessionUser user, string subKey, string valueName);

    /// <summary>Записать DWORD, если ключ есть. true — записано.</summary>
    bool SetUserDword(PlayerSessionUser user, string subKey, string valueName, int value);
}

/// <param name="Cleared">Пункты каталога, по которым что-то нашлось и стёрто.</param>
/// <param name="Failed">Пункты, где стереть не вышло: файл занят, нет прав.</param>
/// <param name="NotRunReason">Уборки не было вовсе — и почему.</param>
public sealed record SessionCleanupOutcome(
    int ClosedApps,
    IReadOnlyList<string> Cleared,
    IReadOnlyList<string> Failed,
    string? NotRunReason = null)
{
    public static SessionCleanupOutcome NotRun(string reason) => new(0, [], [], reason);

    public string Describe()
    {
        if (NotRunReason is not null)
        {
            return $"cleanup skipped: {NotRunReason}";
        }

        var parts = new List<string> { $"closed {ClosedApps} app(s)" };
        parts.Add(Cleared.Count > 0 ? $"cleared {string.Join(", ", Cleared)}" : "nothing to clear");
        if (Failed.Count > 0)
        {
            parts.Add($"could not clear {string.Join(", ", Failed)}");
        }

        return string.Join("; ", parts);
    }
}

public interface ISessionCleanup
{
    /// <summary>Сессия кончилась и ПК заперт: закрыть программы игрока и стереть следы.</summary>
    /// <param name="sessionStartedAtUtc">Когда ПК открылся игроку; null — неизвестно, закрываются только программы каталога.</param>
    Task<SessionCleanupOutcome> RunAsync(DateTimeOffset? sessionStartedAtUtc, CancellationToken cancellationToken);
}

/// <summary>
/// Уборка после сессии (спека оболочки, §6.4): сначала закрыть, потом стирать — открытый Steam
/// держит свои файлы и перепишет их при выходе. Каждый шаг переживает отказ соседнего: не
/// стёрся браузер — Steam всё равно стирается, а итог честно называет, что не вышло.
/// </summary>
public sealed class SessionCleanup(
    IPlayerSessionHost host,
    IProtectionEnforcer protection,
    ILogger<SessionCleanup> logger,
    Func<TimeSpan, CancellationToken, Task>? delay = null) : ISessionCleanup
{
    /// <summary>Сколько ждать, пока закрытые программы действительно выйдут.</summary>
    internal static readonly TimeSpan ExitWait = TimeSpan.FromSeconds(5);

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(250);

    private const int DeleteAttempts = 3;

    public async Task<SessionCleanupOutcome> RunAsync(DateTimeOffset? sessionStartedAtUtc, CancellationToken cancellationToken)
    {
        if (!host.IsSupported)
        {
            return SessionCleanupOutcome.NotRun("needs Windows");
        }

        var user = host.ConsoleUser();
        if (user is null)
        {
            return SessionCleanupOutcome.NotRun("no one is signed in to Windows");
        }

        var items = protection.ClearAfterSession;
        var closed = await CloseAppsAsync(user, sessionStartedAtUtc, SessionTraceCatalog.ProcessesToClose(items), cancellationToken);

        var cleared = new HashSet<string>(StringComparer.Ordinal);
        var failed = new HashSet<string>(StringComparer.Ordinal);
        var steamDirectory = host.SteamDirectory(user);
        foreach (var target in SessionTraceCatalog.Targets(items, user.ProfileDirectory, steamDirectory))
        {
            var root = target.Path.StartsWith(user.ProfileDirectory, StringComparison.OrdinalIgnoreCase)
                ? user.ProfileDirectory
                : steamDirectory;
            var result = await ClearAsync(target, root, cancellationToken);
            if (result == ClearResult.Cleared)
            {
                cleared.Add(target.Item);
            }
            else if (result == ClearResult.Failed)
            {
                failed.Add(target.Item);
            }
        }

        foreach (var trace in SessionTraceCatalog.RegistryTraces(items))
        {
            try
            {
                var changed = trace.SetDword is { } value
                    ? host.SetUserDword(user, trace.SubKey, trace.ValueName, value)
                    : host.DeleteUserValue(user, trace.SubKey, trace.ValueName);
                if (changed && trace.SetDword is null)
                {
                    cleared.Add(trace.Item);
                }
            }
            catch (Exception exception) when (exception is UnauthorizedAccessException or IOException or System.Security.SecurityException)
            {
                logger.LogWarning(exception, "Could not clear {Value} in the player's registry.", trace.ValueName);
                failed.Add(trace.Item);
            }
        }

        var outcome = new SessionCleanupOutcome(
            closed,
            SessionTraceNames.All.Where(cleared.Contains).ToList(),
            SessionTraceNames.All.Where(failed.Contains).ToList());
        logger.LogInformation("Session cleanup: {Outcome}.", outcome.Describe());
        return outcome;
    }

    private async Task<int> CloseAppsAsync(
        PlayerSessionUser user,
        DateTimeOffset? sessionStartedAtUtc,
        IReadOnlySet<string> alwaysClose,
        CancellationToken cancellationToken)
    {
        var protectedRoots = host.ProtectedRoots;
        var closing = host.Processes(user)
            .Where(process => SessionProcessPolicy.ShouldClose(process, sessionStartedAtUtc, alwaysClose, protectedRoots))
            .ToList();
        var terminated = closing.Where(process => host.TryTerminate(process.ProcessId)).ToList();

        var waited = TimeSpan.Zero;
        while (terminated.Any(process => host.IsRunning(process.ProcessId)) && waited < ExitWait)
        {
            await (delay ?? Task.Delay)(PollInterval, cancellationToken);
            waited += PollInterval;
        }

        foreach (var process in closing.Except(terminated))
        {
            logger.LogWarning("Could not close {Image} ({ProcessId}) after the session.", process.ImageName, process.ProcessId);
        }

        return terminated.Count;
    }

    private enum ClearResult
    {
        NothingThere,
        Cleared,
        Failed
    }

    private async Task<ClearResult> ClearAsync(TraceTarget target, string? root, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return Clear(target, root);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                if (attempt == DeleteAttempts)
                {
                    logger.LogWarning(exception, "Could not clear {Path} after the session.", target.Path);
                    return ClearResult.Failed;
                }

                // Программа только что закрыта, и Windows ещё держит её файлы.
                await (delay ?? Task.Delay)(TimeSpan.FromMilliseconds(300), cancellationToken);
            }
        }
    }

    private ClearResult Clear(TraceTarget target, string? root)
    {
        if (root is null)
        {
            return ClearResult.NothingThere;
        }

        switch (target.Kind)
        {
            case TraceTargetKind.Directory when SessionTraceCatalog.IsInside(target.Path, root) && Directory.Exists(target.Path):
                Directory.Delete(target.Path, recursive: true);
                return ClearResult.Cleared;

            case TraceTargetKind.File when SessionTraceCatalog.IsInside(target.Path, root) && File.Exists(target.Path):
                File.Delete(target.Path);
                return ClearResult.Cleared;

            case TraceTargetKind.FilesMatching when Directory.Exists(target.Path):
                var files = Directory.GetFiles(target.Path, target.Pattern ?? string.Empty, SearchOption.TopDirectoryOnly)
                    .Where(file => SessionTraceCatalog.IsInside(file, root))
                    .ToList();
                foreach (var file in files)
                {
                    File.Delete(file);
                }

                return files.Count > 0 ? ClearResult.Cleared : ClearResult.NothingThere;

            case TraceTargetKind.SteamConnectCache when SessionTraceCatalog.IsInside(target.Path, root) && File.Exists(target.Path):
                var text = File.ReadAllText(target.Path);
                var trimmed = VdfText.RemoveBlocks(text, "ConnectCache", out var removed);
                if (removed == 0)
                {
                    return ClearResult.NothingThere;
                }

                File.WriteAllText(target.Path, trimmed);
                return ClearResult.Cleared;

            default:
                return ClearResult.NothingThere;
        }
    }
}
