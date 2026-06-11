using System.IO;

namespace AFK4.Player.Shell;

// Minimal best-effort crash log for the kiosk shell, written next to the agent log
// (%ProgramData%\AFK4\logs\player-shell.log). The shell has no other logging sink, so a
// startup failure otherwise only surfaces in the Windows Event Viewer. Failures here are
// swallowed: logging must never be the reason the shell can't start.
public static class PlayerShellStartupLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "logs",
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
