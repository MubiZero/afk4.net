namespace AFK4.SetupWizard.Web;

public sealed record SetupWizardWebShellLaunchTarget(
    Uri Source,
    string Mode,
    string? LocalFolderPath)
{
    public bool UsesLocalFolder => !string.IsNullOrWhiteSpace(LocalFolderPath);
}
