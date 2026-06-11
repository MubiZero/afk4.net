using System.IO;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Locates an MSI bundled next to the wizard executable under `…\Setup Wizard\payload\`.
/// The agent MSI ships both the Player Shell (for gaming PCs) and the Operator App (for
/// cashier/manager workstations) here so the wizard can install the right one per role.
/// Returns null when the requested MSI is absent.
/// </summary>
public sealed class SetupWizardPayloadResolver
{
    public const string PlayerShellMsiFileName = "AFK4.Player.Shell.msi";
    public const string OperatorAppMsiFileName = "AFK4.Operator.App.msi";

    private readonly Func<string, bool> fileExists;
    private readonly Func<string, string> resolvePath;

    public SetupWizardPayloadResolver(string baseDirectory)
        : this(File.Exists, fileName => Path.Combine(baseDirectory, "payload", fileName))
    {
    }

    // Test seam: inject existence check + path so resolution is platform-independent.
    public SetupWizardPayloadResolver(Func<string, bool> fileExists, Func<string, string> resolvePath)
    {
        this.fileExists = fileExists;
        this.resolvePath = resolvePath;
    }

    public string? ResolvePlayerShellMsiPath() => ResolveMsiPath(PlayerShellMsiFileName);

    public string? ResolveOperatorAppMsiPath() => ResolveMsiPath(OperatorAppMsiFileName);

    private string? ResolveMsiPath(string fileName)
    {
        var path = resolvePath(fileName);
        return fileExists(path) ? path : null;
    }
}
