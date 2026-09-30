using System.IO;

namespace AFK4.Player.Shell;

// Minimal best-effort crash log for the kiosk shell (%ProgramData%\AFK4\logs\player\player-shell.log).
// The shell runs as the player, so it writes only to logs\player — the one folder under AFK4 the agent
// leaves writable for players (AgentDataFolder). AFK4_PLAYER_SHELL_LOG overrides the path: tests point
// it at a temp file, otherwise a Windows test run writes its fakes into the real PC's shell log.
// Failures here are swallowed: logging must never be the reason the shell can't start.
public static class PlayerShellStartupLog
{
    private static readonly string LogPath =
        Environment.GetEnvironmentVariable("AFK4_PLAYER_SHELL_LOG") is { Length: > 0 } overridePath
            ? overridePath
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                "AFK4",
                "logs",
                "player",
                "player-shell.log");

    public static void Write(string message, Exception? exception = null)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            var line = exception is null
                ? $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}"
                : $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}{exception}{Environment.NewLine}";
            File.AppendAllText(LogPath, line);
        }
        catch
        {
            // Never let logging break startup.
        }
    }
}
