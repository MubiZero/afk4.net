using AFK4.Agent.Service.Enforcement;
using AFK4.Agent.Service.Games;
using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;
using AFK4.Shared.Contracts.Sessions;
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
    ShellHeartbeatSnapshot heartbeatSnapshot,
    ShellWarningStore shellWarningStore,
    TimeProvider timeProvider,
    IProtectionEnforcer? protection = null,
    ILauncherCatalog? catalog = null,
    AFK4.Agent.Service.Power.IdleShutdownMonitor? idleShutdown = null,
    AFK4.Agent.Service.Showcase.IShowcaseSource? showcase = null) : IPlayerShellStateBuilder
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

        // Сессия — та, о которой говорит и аренда, и сервер в сердцебиении: иначе сразу после
        // старта новой сессии экран взял бы конец предыдущей.
        var liveSession = heartbeatSnapshot.LiveSession is { } live
            && (lease is null || live.SessionId == lease.SessionId)
            ? live
            : null;
        int? remainingSeconds = RemainingSeconds(lease, liveSession, isOnline, now);
        var state = ResolveState(runtimeState.State, remainingSeconds, isOnline);
        var isGraceMode = string.Equals(state, PlayerShellStateNames.Grace, StringComparison.Ordinal);
        var inMaintenance = string.Equals(state, PlayerShellStateNames.Maintenance, StringComparison.Ordinal);
        var threshold = agentOptions.ShellWarningThresholdSeconds;
        var sessionId = lease?.SessionId ?? runtimeState.ActiveSessionId;
        var sessionOwner = heartbeatSnapshot.SessionOwnerFor(sessionId);

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
            LauncherApps: catalog is null
                ? CreateLauncherApps(agentOptions)
                : CreateLauncherApps(catalog, sessionOwner?.PlayerAge),
            ClubRules: protection?.Profile.ClubRules,
            IdleShutdownAtUtc: idleShutdown?.ShutdownAtUtc,
            Showcase: ShowcaseFor(state),
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
            SessionOwnerKind: sessionOwner?.Kind,
            SessionOwnerPlayerAccountId: sessionOwner?.PlayerAccountId,
            Features: heartbeatSnapshot.Features,
            // Кто и когда — только в обслуживании: вне его полосе нечего писать.
            MaintenanceSinceUtc: inMaintenance ? heartbeatSnapshot.MaintenanceSinceUtc : null,
            MaintenanceByName: inMaintenance ? heartbeatSnapshot.MaintenanceByName : null,
            // В обслуживании технику нужны и командная строка, и реестр — окна не закрываются.
            BlockedWindows: inMaintenance || protection is null ? [] : protection.BlockedWindows,
            SessionStartedAtUtc: lease is null ? null : liveSession?.StartedAtUtc,
            SessionEndsAtUtc: lease is null ? null : liveSession?.EndsAtUtc);
    }

    /// <summary>
    /// Сколько осталось: до конца сессии, а не до конца аренды — аренда подписана на 15 минут и
    /// продлевается, пока сессия идёт. Без связи сессия живёт не дольше подписанной аренды, поэтому
    /// берётся ближайшее из двух. У открытого счёта конца нет: на связи — null (экран показывает,
    /// сколько идёт), без связи — остаток аренды.
    /// </summary>
    public static int? RemainingSeconds(SessionLeaseDto? lease, DeviceLiveSessionDto? liveSession, bool isOnline, DateTimeOffset now)
    {
        if (lease is null)
        {
            return null;
        }

        DateTimeOffset? until = liveSession?.EndsAtUtc is { } endsAt
            ? isOnline ? endsAt : Min(endsAt, lease.ExpiresAtUtc)
            : isOnline && liveSession is not null ? null : lease.ExpiresAtUtc;
        return until is { } end ? Math.Max(0, (int)(end - now).TotalSeconds) : null;
    }

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left < right ? left : right;

    /// <summary>
    /// Реклама платформы — только на свободном ПК (PRD): во время сессии, в её последнюю минуту и в
    /// обслуживании её в состоянии нет, даже если экран по ошибке решит показать витрину.
    /// </summary>
    private IReadOnlyList<AFK4.Shared.Contracts.Showcase.ShowcaseCardDto>? ShowcaseFor(string state)
    {
        var cards = showcase?.Cards();
        return cards is null || state is PlayerShellStateNames.Locked or PlayerShellStateNames.Offline
            ? cards
            : cards.Where(card => card.Kind != AFK4.Shared.Contracts.Showcase.ShowcaseCardKindNames.Ad).ToList();
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
                Category: app.Category?.Trim() ?? string.Empty,
                IconUri: null,
                IsAvailable: File.Exists(app.ExecutablePath)))
            .ToList();

    /// <summary>
    /// Библиотека клуба: лаунчера на ПК нет — плитка видна недоступной, а не пропадает. Игра старше
    /// игрока видна запертой: он знает, что она есть, и почему не для него.
    /// </summary>
    private static IReadOnlyList<LauncherAppDto> CreateLauncherApps(ILauncherCatalog catalog, int? playerAge) =>
        catalog.Entries()
            .Select(entry => new LauncherAppDto(
                AppId: entry.AppId,
                DisplayName: entry.DisplayName,
                Category: entry.Category,
                IconUri: entry.IconUri,
                IsAvailable: entry.ExecutablePath is not null && File.Exists(entry.ExecutablePath),
                MinAge: entry.MinAge,
                AgeLocked: GameAgeGate.IsLocked(entry.MinAge, playerAge)))
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
