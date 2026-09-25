namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// Мост хост ↔ интерфейс оболочки, версия 2 (спека оболочки, §4.4). Конверт запроса и ответа —
/// общий, из @afk4/host-bridge; здесь — имена и тела. Записи C# дают типы и хосту, и странице:
/// две руками написанные копии однажды разошлись бы.
/// </summary>
public static class ShellBridgeRequestTypeNames
{
    /// <summary>
    /// Страница загрузилась и слушает. Ответ — ShellSnapshotDto: всё, что хост уже знает. Без
    /// этого состояние, отправленное до того, как React подписался, терялось бы, и экран ждал бы
    /// следующего пульса агента.
    /// </summary>
    public const string ShellReady = "shell.ready";

    /// <summary>Войти номером и ПИН-кодом — через агента, токены привязаны к этому ПК.</summary>
    public const string AuthSignIn = "auth.signIn";

    public const string AuthSignOut = "auth.signOut";

    /// <summary>Запустить игру из библиотеки клуба.</summary>
    public const string AppLaunch = "app.launch";

    /// <summary>Позвать администратора к этому ПК.</summary>
    public const string AssistCall = "assist.call";

    public const string SystemSetVolume = "system.setVolume";

    public const string SystemSetMicMuted = "system.setMicMuted";

    public const string SystemSetLayout = "system.setLayout";

    /// <summary>Язык интерфейса выбран на экране: хост запоминает его до выхода игрока.</summary>
    public const string UiSetLocale = "ui.setLocale";

    public const string ShowcaseImpression = "showcase.impression";

    /// <summary>Кнопка «Вернуть в зал» на полосе обслуживания.</summary>
    public const string MaintenanceReturn = "maintenance.return";
}

public static class ShellBridgeEventTypeNames
{
    /// <summary>Состояние ПК от агента — PlayerShellStateDto.</summary>
    public const string StateChanged = "state.changed";

    /// <summary>Вошёл ли игрок на этом ПК — ShellAuthStateDto.</summary>
    public const string AuthChanged = "auth.changed";

    /// <summary>Мышь или клавиатура тронуты: витрина уступает место окну входа.</summary>
    public const string InputActivity = "input.activity";

    /// <summary>Тишина дольше порога: окно входа закрывается, вошедший выходит.</summary>
    public const string InputIdle = "input.idle";

    /// <summary>Игра на переднем плане — ShellGameForegroundDto: страница засыпает, чтобы не отнимать кадр.</summary>
    public const string GameForeground = "game.foreground";

    /// <summary>Громкость, микрофон, раскладка — ShellSystemStateDto.</summary>
    public const string SystemChanged = "system.changed";

    public const string ShowcaseChanged = "showcase.changed";
}

public static class ShellBridgeErrorCodeNames
{
    /// <summary>Номер или ПИН-код не подошли.</summary>
    public const string SignInRefused = "sign_in_refused";

    /// <summary>С этого ПК слишком много неудачных входов.</summary>
    public const string TooManyAttempts = "too_many_attempts";

    /// <summary>На ПК идёт чужая сессия.</summary>
    public const string SessionNotYours = "session_not_yours";

    /// <summary>Агента нет на связи — войти и запустить игру сейчас нельзя.</summary>
    public const string AgentUnavailable = "agent_unavailable";

    /// <summary>Агент на месте, а до сервера клуба не достучался.</summary>
    public const string PlatformUnreachable = "platform_unreachable";

    /// <summary>Такого хост пока не умеет: запрос из более новой страницы или раздел следующего этапа.</summary>
    public const string NotSupported = "not_supported";

    /// <summary>Windows не дала поменять звук, микрофон или раскладку — например, нет устройства.</summary>
    public const string SystemUnavailable = "system_unavailable";
}

public sealed record ShellAuthSignInRequest(
    string Phone,
    string Pin);

/// <summary>Кто вошёл на этом ПК. Токены страница не видит: их держит хост.</summary>
public sealed record ShellAuthStateDto(
    bool SignedIn,
    string? DisplayName = null,
    Guid? PlayerAccountId = null);

public sealed record ShellLaunchRequest(string AppId);

public sealed record ShellGameForegroundDto(bool Active);

/// <summary>
/// Звук, микрофон и раскладка ПК. Пусто — у ПК этого нет или Windows не ответила (нет
/// микрофона, звуковая карта отключена): страница прячет кнопку, а не показывает выдуманное.
/// </summary>
public sealed record ShellSystemStateDto(
    // 0–100.
    int? Volume,
    bool? MicMuted,
    // Раскладка клавиатуры — одно из ShellKeyboardLayoutNames.
    string? Layout);

public static class ShellKeyboardLayoutNames
{
    public const string Russian = "RU";

    public const string English = "EN";

    public const string Tajik = "TG";
}

public sealed record ShellSetVolumeRequest(
    // 0–100.
    int Volume);

public sealed record ShellSetMicMutedRequest(bool MicMuted);

public sealed record ShellSetLayoutRequest(
    // Одно из ShellKeyboardLayoutNames.
    string Layout);

/// <summary>Всё, что хост знает к моменту, когда страница загрузилась.</summary>
public sealed record ShellSnapshotDto(
    // Пусто — агент ещё не прислал состояния: экран говорит «подключаемся к ПК».
    PlayerShellStateDto? State,
    ShellAuthStateDto Auth,
    ShellSystemStateDto? System = null);
