namespace AFK4.OrganizationAdmin.Web;

public sealed record OrganizationAdminWebShellLaunchTarget(
    Uri Source,
    string Mode,
    string? LocalFolderPath)
{
    public bool UsesLocalFolder => !string.IsNullOrWhiteSpace(LocalFolderPath);
}
