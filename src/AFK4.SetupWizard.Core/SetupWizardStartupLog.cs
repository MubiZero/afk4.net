using System.IO;

namespace AFK4.SetupWizard.Core;

// Minimal best-effort crash log for the setup wizard, written next to the agent log
// (%ProgramData%\AFK4\logs\setup-wizard.log). The wizard has no other logging sink, so a
// startup/enrollment failure otherwise only surfaces as a raw .NET crash dialog. Failures here
// are swallowed: logging must never be the reason the wizard can't run.
public static class SetupWizardStartupLog
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "AFK4",
        "logs",
        "setup-wizard.log");

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
            // Never let logging break the wizard.
        }
    }
}
