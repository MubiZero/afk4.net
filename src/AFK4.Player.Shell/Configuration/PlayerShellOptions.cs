namespace AFK4.Player.Shell.Configuration;

public sealed class PlayerShellOptions
{
    public string PipeName { get; init; } = "afk4-player-shell";

    public string CommandPipeName { get; init; } = "afk4-player-shell-commands";

    public int PipeConnectionTimeoutMilliseconds { get; init; } = 500;

    public int ReconnectDelayMilliseconds { get; init; } = 500;

    public string ApiBaseUrl { get; init; } =
        Environment.GetEnvironmentVariable("AFK4_PLATFORM_API_BASE_URL") ?? "https://api.afk4.net";
}
