using System.IO;

namespace AFK4.Operator.App;

// Minimal best-effort crash log for the operator host. The WebView host otherwise surfaces a
// startup or dispatcher failure only in the Windows Event Viewer. Prefers the per-user operator
// log directory and falls back to the machine-wide AFK4 log directory. Failures here are
// swallowed: logging must never be the reason the host can't start.
public static class OperatorStartupLog
{
    private static readonly string LogPath = ResolveLogPath();

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

    private static string ResolveLogPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrWhiteSpace(localAppData))
        {
            return Path.Combine(localAppData, "AFK4", "Operator", "logs", "operator.log");
        }

        var commonAppData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        return Path.Combine(commonAppData, "AFK4", "logs", "operator.log");
    }
}
