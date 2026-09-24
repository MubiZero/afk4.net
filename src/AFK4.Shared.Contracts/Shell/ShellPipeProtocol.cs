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
}

public static class ShellPipeRequestTypeNames
{
    /// <summary>Запустить игру из списка клуба. В теле — <c>appId</c>.</summary>
    public const string Launch = "launch";

    /// <summary>Позвать администратора к этому ПК.</summary>
    public const string Assist = "assist";
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
}
