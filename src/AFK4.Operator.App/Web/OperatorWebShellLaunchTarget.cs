namespace AFK4.Operator.App.Web;

public sealed record OperatorWebShellLaunchTarget(
    Uri Source,
    string Mode,
    string? LocalFolderPath)
{
    public bool UsesLocalFolder => !string.IsNullOrWhiteSpace(LocalFolderPath);
}
