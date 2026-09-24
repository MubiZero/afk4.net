using AFK4.Shared.Contracts.Shell;

namespace AFK4.Player.Shell.Configuration;

public sealed class PlayerShellOptions
{
    public string ShellPipeName { get; init; } = ShellPipeProtocol.DefaultPipeName;

    public int ConnectTimeoutMilliseconds { get; init; } = 2000;

    /// <summary>Три пульса агента: тишина дольше — связь мёртвая, даже если канал открыт.</summary>
    public int SilenceTimeoutMilliseconds { get; init; } = 15_000;

    /// <summary>
    /// Сколько ждать ответа агента. Меньше, чем веб-слой ждёт хост (15 с): игрок должен получить
    /// ответ «агент не ответил», а не безымянный таймаут интерфейса.
    /// </summary>
    public int RequestTimeoutMilliseconds { get; init; } = 12_000;

    public string ApiBaseUrl { get; init; } =
        Environment.GetEnvironmentVariable("AFK4_PLATFORM_API_BASE_URL") ?? "https://api.afk4.net";
}
