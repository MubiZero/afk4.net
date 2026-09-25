namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// Канал между агентом и хостом оболочки, версия 2 (спека оболочки, §4.2).
///
/// Раньше каналов было два, и оба открывали соединение на каждое сообщение: хост спрашивал
/// состояние раз в 500 мс, а за запуском игры ходил отдельным соединением. Теперь канал один и
/// живёт, пока жив хост: агент сам присылает состояние, как только оно изменилось.
/// </summary>
public static class ShellPipeProtocol
{
    public const int Version = 2;

    public const string DefaultPipeName = "afk4-shell-v2";

    /// <summary>
    /// Предел одного кадра. Самое длинное сообщение — состояние со списком игр клуба, это
    /// десятки килобайт. Мегабайт с запасом, а длина из сломанного потока не заставит выделить
    /// гигабайт.
    /// </summary>
    public const int MaxFrameBytes = 1024 * 1024;
}

public static class ShellPipeMessageTypeNames
{
    /// <summary>Хост представляется первым; без этого агент ничего не шлёт.</summary>
    public const string Hello = "hello";

    /// <summary>Агент прощается: версия протокола не та или хост не из той сессии.</summary>
    public const string Bye = "bye";

    public const string State = "state";

    public const string Request = "request";

    public const string Reply = "reply";

    /// <summary>Агент передаёт хосту команду клуба: выйти из аккаунта игрока или показать сообщение.</summary>
    public const string Command = "command";

    /// <summary>
    /// Игрок вошёл: агент отдаёт хосту токены. Один кадр на оба пути — ПИН-код и QR: вход по QR
    /// приходит без запроса хоста, и отвечать на него нечем, кроме отдельного кадра.
    /// </summary>
    public const string Auth = "auth";
}

public static class ShellPipeRequestTypeNames
{
    /// <summary>Запустить игру из списка клуба. В теле — <c>appId</c>.</summary>
    public const string Launch = "launch";

    /// <summary>Позвать администратора к этому ПК.</summary>
    public const string Assist = "assist";

    /// <summary>
    /// Войти номером и ПИН-кодом. В теле — <c>phone</c> и <c>pin</c>. Удачный ответ пуст: токены
    /// приходят кадром <see cref="ShellPipeMessageTypeNames.Auth"/>.
    /// </summary>
    public const string SignInPin = "signIn.pin";

    /// <summary>
    /// «Вернуть в зал» с самого ПК (спека оболочки, §6.5): агент говорит серверу и закрывает
    /// рабочий стол техника. Тело пустое.
    /// </summary>
    public const string MaintenanceReturn = "maintenance.return";

    /// <summary>
    /// За ПК кто-то есть: тронуты мышь или клавиатура. Не чаще раза в 20 секунд; по нему агент не
    /// выключает простаивающий ПК под рукой человека и отменяет уже назначенное выключение. Тело пустое.
    /// </summary>
    public const string Activity = "activity";

    /// <summary>
    /// Рекламная карточка витрины отстояла на экране. В теле — <c>cardId</c> и <c>shownMs</c>.
    /// Агент считает только рекламу и только на свободном ПК.
    /// </summary>
    public const string ShowcaseImpression = "showcase.impression";
}

public static class ShellPipeErrorCodeNames
{
    public const string ProtocolMismatch = "protocol_mismatch";

    /// <summary>
    /// Хост подключился не из консольной сессии — например, по удалённому рабочему столу.
    /// Состояние этого ПК и запуск игр принадлежат тому, кто сидит за монитором.
    /// </summary>
    public const string WrongSession = "wrong_session";

    public const string InvalidPayload = "invalid_payload";

    public const string UnknownRequest = "unknown_request";

    /// <summary>Игры запускаются только во время сессии.</summary>
    public const string NoSession = "no_session";

    public const string AppNotAllowed = "app_not_allowed";

    /// <summary>Игра в списке клуба, но её файла на этом ПК нет.</summary>
    public const string AppMissing = "app_missing";

    public const string LaunchFailed = "launch_failed";

    /// <summary>До платформы не достучались — стойка о вызове не узнала.</summary>
    public const string PlatformUnreachable = "platform_unreachable";

    /// <summary>Хосту некуда отправить запрос: агента нет на другом конце канала.</summary>
    public const string AgentUnavailable = "agent_unavailable";

    /// <summary>Номер или ПИН-код не подошли. Те же имена, что у сервера и моста к странице.</summary>
    public const string SignInRefused = "sign_in_refused";

    public const string TooManyAttempts = "too_many_attempts";

    /// <summary>На ПК идёт чужая сессия: вход верный, но открыть вошедшему нечего.</summary>
    public const string SessionNotYours = "session_not_yours";

    /// <summary>Клуб закрыл этот ПК на обслуживание — вход на нём закрыт.</summary>
    public const string DeviceInMaintenance = "device_in_maintenance";
}
