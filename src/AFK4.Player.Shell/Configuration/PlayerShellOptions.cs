namespace AFK4.Player.Shell.Configuration;

public sealed class PlayerShellOptions
{
    public string PipeName { get; init; } = "afk4-player-shell";

    public int PipeConnectionTimeoutMilliseconds { get; init; } = 500;

    public int ReconnectDelayMilliseconds { get; init; } = 500;
}
