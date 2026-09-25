using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Games;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Shell;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Shell;

/// <summary>Собирает то, что игрок видит на экране, из того, что агент знает прямо сейчас.</summary>
public interface IPlayerShellStateBuilder
{
    PlayerShellStateDto Build();
}

/// <summary>
/// Раньше состояние собирал работник раз в сердцебиение и всегда писал «на связи»: запертый ПК
/// без сети показывал код посадки, который сервер уже не примет. Теперь связь выводится из
/// времени последнего удачного контакта, а собрать состояние можно в любой момент — канал
/// делает это, когда что-то изменилось.
/// </summary>
public sealed class PlayerShellStateBuilder(
    IOptions<AgentOptions> options,
    ISessionLeaseStore leaseStore,
    IAgentRuntimeStateStore runtimeStateStore,
    IOfflineGraceState offlineGraceState,
    IShellHeartbeatSnapshot heartbeatSnapshot,
    IShellWarningStore shellWarningStore,
    TimeProvider timeProvider,
    IProtectionEnforcer? protection = null,
    ILauncherCatalog? catalog = null) : IPlayerShellStateBuilder
{
    /// <summary>Последняя минута сессии — отдельное состояние: экран готовит игрока к концу.</summary>
    public const int EndingThresholdSeconds = 60;

    public PlayerShellStateDto Build()
    {
        var agentOptions = options.Value;
        var now = timeProvider.GetUtcNow();
        var runtimeState = runtimeStateStore.Current;
        var lease = leaseStore.Current;
        var lastContactUtc = offlineGraceState.LastSuccessfulContactUtc;
        var isOnline = ShellConnectivity.IsOnline(lastContactUtc, heartbeatSnapshot.IntervalSeconds, now);

        int? remainingSeconds = lease is null
            ? null
            : Math.Max(0, (int)(lease.ExpiresAtUtc - now).TotalSeconds);
        var state = ResolveState(runtimeState.State, remainingSeconds, isOnline);
        var isGraceMode = string.Equals(state, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        var inMaintenance = string.Equals(state, PlayerShellStateNames.Maintenance, StringComparison.Ordinal);
        var threshold = agentOptions.ShellWarningThresholdSeconds;
        var sessionId = lease?.SessionId ?? runtimeState.ActiveSessionId;

        // Предупреждение живёт ровно столько, сколько сессия, к которой оно пришло.
        shellWarningStore.ForgetUnless(sessionId);

        return new PlayerShellStateDto(
            OrganizationId: agentOptions.OrganizationId,
            BranchId: agentOptions.BranchId,
            DeviceId: agentOptions.DeviceId,
            State: state,
            SessionId: sessionId,
            LeaseExpiresAtUtc: lease?.ExpiresAtUtc ?? runtimeState.LeaseExpiresAtUtc,
            RemainingSeconds: remainingSeconds,
            IsOnline: isOnline,
            IsGraceMode: isGraceMode,
            WarningThresholdSeconds: threshold,
            Message: CreateMessage(state),
            LauncherApps: catalog is null ? CreateLauncherApps(agentOptions) : CreateLauncherApps(catalog),
            Locale: agentOptions.PreferredLocale,
            WarningKind: ResolveWarning(state, remainingSeconds, threshold, isGraceMode, isOnline),
            // Оформление приходит сердцебиением; значения из конфига остаются запасным вариантом
            // для первого запуска, пока сервер ещё не ответил ни разу.
            Branding: heartbeatSnapshot.Branding ?? (string.IsNullOrWhiteSpace(agentOptions.ClubName)
                ? null
                : new ShellBrandingDto(agentOptions.ClubName!, agentOptions.LogoUrl, agentOptions.AccentColor)),
            SeatingCode: ResolveSeatingCode(state, now, out var seatingCodeExpiresAtUtc),
            SeatingCodeExpiresAtUtc: seatingCodeExpiresAtUtc,
            ObservedAtUtc: now,
            LastContactUtc: lastContactUtc,
            ApiBaseUrl: agentOptions.PlatformBaseUrl.ToString(),
            // Место и права — последние, что сервер назвал: без связи экран всё равно пишет «ПК 07».
            SeatLabel: heartbeatSnapshot.Seat?.Label,
            ZoneName: heartbeatSnapshot.Seat?.ZoneName,
            SessionOwnerKind: heartbeatSnapshot.SessionOwner?.Kind,
            SessionOwnerPlayerAccountId: heartbeatSnapshot.SessionOwner?.PlayerAccountId,
            Features: heartbeatSnapshot.Features,
            // Кто и когда — только в обслуживании: вне его полосе нечего писать.
            MaintenanceSinceUtc: inMaintenance ? heartbeatSnapshot.MaintenanceSinceUtc : null,
            MaintenanceByName: inMaintenance ? heartbeatSnapshot.MaintenanceByName : null,
            // В обслуживании технику нужны и командная строка, и реестр — окна не закрываются.
            BlockedWindows: inMaintenance || protection is null ? [] : protection.BlockedWindows);
    }

    private static string ResolveState(string runtimeState, int? remainingSeconds, bool isOnline) => runtimeState switch
    {
        PlayerShellStateNames.Active when remainingSeconds <= EndingThresholdSeconds => PlayerShellStateNames.Ending,
        // Запертая машина без связи — это «офлайн»: сесть за неё сейчас нельзя, и экран должен
        // сказать это, а не звать человека кодом, который сервер не примет.
        PlayerShellStateNames.Locked when !isOnline => PlayerShellStateNames.Offline,
        _ => runtimeState
    };

    /// <summary>
    /// Код показывается только у свободной машины на связи и только пока он не истёк. Иначе
    /// человек наберёт его в приложении и получит отказ, стоя у ПК.
    /// </summary>
    private string? ResolveSeatingCode(string state, DateTimeOffset now, out DateTimeOffset? expiresAtUtc)
    {
        expiresAtUtc = null;
        if (!string.Equals(state, PlayerShellStateNames.Locked, StringComparison.Ordinal))
        {
            return null;
        }

        var code = heartbeatSnapshot.SeatingCode;
        var codeExpiresAtUtc = heartbeatSnapshot.SeatingCodeExpiresAtUtc;
        if (string.IsNullOrWhiteSpace(code) || codeExpiresAtUtc is not { } expiry || expiry <= now)
        {
            return null;
        }

        expiresAtUtc = expiry;
        return code;
    }

    /// <summary>
    /// Локальная оценка сильнее: связь пропала или время на исходе — это состояние самой машины,
    /// и оно важнее того, что сервер знал минуту назад. А вот когда локально «всё спокойно»
    /// (открытый счёт: остатка секунд нет вовсе), на экран идёт предупреждение сервера — иначе
    /// игрок узнаёт о долге по погасшему экрану.
    /// </summary>
    private string ResolveWarning(string state, int? remainingSeconds, int threshold, bool isGraceMode, bool isOnline)
    {
        var localWarning = PlayerShellWarning.Classify(state, remainingSeconds, threshold, isGraceMode);
        if (!string.Equals(localWarning, PlayerShellWarningKinds.None, StringComparison.Ordinal))
        {
            return localWarning;
        }

        // Сессия идёт по аренде и без сети — но игрок должен знать, что продлить её сейчас нельзя.
        if (!isOnline)
        {
            return PlayerShellWarningKinds.Connectivity;
        }

        return shellWarningStore.Current?.Kind ?? PlayerShellWarningKinds.None;
    }

    /// <summary>
    /// Список игр, который видит игрок. Берётся из той же настройки, по которой агент решает,
    /// что ему разрешено запускать: два разных списка разошлись бы в первый же день. Пункт,
    /// исполняемого файла которого на машине нет, показывается недоступным, а не прячется —
    /// «игра была вчера, а сегодня её нет» должно быть видно и игроку, и клубу.
    /// </summary>
    private static IReadOnlyList<LauncherAppDto> CreateLauncherApps(AgentOptions agentOptions) =>
        agentOptions.LauncherApps
            .Where(app => app.IsEnabled
                && !string.IsNullOrWhiteSpace(app.AppId)
                && !string.IsNullOrWhiteSpace(app.ExecutablePath))
            .Select(app => new LauncherAppDto(
                AppId: app.AppId,
                DisplayName: string.IsNullOrWhiteSpace(app.DisplayName) ? app.AppId : app.DisplayName,
                Category: string.IsNullOrWhiteSpace(app.Category) ? "Games" : app.Category,
                IconUri: null,
                IsAvailable: File.Exists(app.ExecutablePath)))
            .ToList();

    /// <summary>Библиотека клуба: лаунчера на ПК нет — плитка видна недоступной, а не пропадает.</summary>
    private static IReadOnlyList<LauncherAppDto> CreateLauncherApps(ILauncherCatalog catalog) =>
        catalog.Entries()
            .Select(entry => new LauncherAppDto(
                AppId: entry.AppId,
                DisplayName: entry.DisplayName,
                Category: entry.Category,
                IconUri: entry.IconUri,
                IsAvailable: entry.ExecutablePath is not null && File.Exists(entry.ExecutablePath),
                MinAge: entry.MinAge))
            .ToList();

    private static string CreateMessage(string state) => state switch
    {
        PlayerShellStateNames.Active => "Session is active.",
        PlayerShellStateNames.Grace => "Connection lost. Active session continues within the signed lease.",
        PlayerShellStateNames.Ending => "Session is ending.",
        PlayerShellStateNames.Maintenance => "This PC is under maintenance.",
        PlayerShellStateNames.Offline => "No connection to the club. This PC cannot start a session right now.",
        PlayerShellStateNames.Error => "This PC needs operator attention.",
        _ => "This PC is locked."
    };
}

/// <summary>
/// «На связи» — это свежий удачный контакт, а не константа. Окно идёт за интервалом, который
/// сервер назвал сам: два пропущенных сердцебиения плюс запас на медленный ответ.
/// </summary>
public static class ShellConnectivity
{
    private const int UnknownIntervalSeconds = 10;
    private const int MinimumWindowSeconds = 10;
    private const int SlackSeconds = 5;

    public static bool IsOnline(DateTimeOffset? lastContactUtc, int? intervalSeconds, DateTimeOffset nowUtc)
    {
        if (lastContactUtc is not { } lastContact)
        {
            return false;
        }

        var interval = intervalSeconds is > 0 ? intervalSeconds.Value : UnknownIntervalSeconds;
        var window = TimeSpan.FromSeconds(Math.Max(2 * interval, MinimumWindowSeconds) + SlackSeconds);
        return nowUtc - lastContact <= window;
    }
}
