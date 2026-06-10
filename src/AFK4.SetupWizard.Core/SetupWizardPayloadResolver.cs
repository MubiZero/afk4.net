using System.IO;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Locates the Player Shell MSI bundled next to the wizard executable
/// (`…\Setup Wizard\payload\AFK4.Player.Shell.msi`). Returns null when absent.
/// </summary>
public sealed class SetupWizardPayloadResolver
{
    public const string PlayerShellMsiFileName = "AFK4.Player.Shell.msi";

    private readonly Func<string, bool> fileExists;
    private readonly Func<string> resolvePath;

    public SetupWizardPayloadResolver(string baseDirectory)
        : this(File.Exists, () => Path.Combine(baseDirectory, "payload", PlayerShellMsiFileName))
    {
    }

    // Test seam: inject existence check + path so resolution is platform-independent.
    public SetupWizardPayloadResolver(Func<string, bool> fileExists, Func<string> resolvePath)
    {
        this.fileExists = fileExists;
        this.resolvePath = resolvePath;
    }

    public string? ResolvePlayerShellMsiPath()
    {
        var path = resolvePath();
        return fileExists(path) ? path : null;
    }
}
