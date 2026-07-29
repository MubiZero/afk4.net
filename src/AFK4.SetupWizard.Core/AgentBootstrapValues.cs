using System.Security.Cryptography;
using System.Text;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Single source of truth for the Agent bootstrap configuration the wizard hands off after
/// enrollment. Keys are the <c>AgentOptions</c> property names (no prefix); the environment
/// writer emits them as <c>Agent__{Key}</c> and the file writer nests them under "Agent".
/// Keeping one builder stops the two delivery channels from drifting apart.
/// </summary>
public static class AgentBootstrapValues
{
    public static IReadOnlyDictionary<string, string> Build(SetupWizardBootstrapConfig config, string machineName)
    {
        var programFiles = ProgramFilesPath();
        var powershell = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var helperDirectory = Path.Combine(programFiles, @"AFK4\Update Helpers");
        var organizationAdminPipeName = $"afk4-organization-admin-updates-{config.DeviceId:N}";
        var organizationAdminCoordinationSecret = DeriveOrganizationAdminCoordinationSecret(config);

        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["PlatformBaseUrl"] = config.ApiBaseUrl.TrimEnd('/'),
            ["OrganizationId"] = config.OrganizationId.ToString("D"),
            ["BranchId"] = config.BranchId.ToString("D"),
            ["DeviceId"] = config.DeviceId.ToString("D"),
            ["MachineName"] = machineName,
            ["DeviceRole"] = string.IsNullOrWhiteSpace(config.Role) ? DeviceRoleNames.GamingPc : config.Role,
            ["DeviceCredentialSecret"] = config.CredentialSecret,
            ["LeaseSigningPublicKeyPem"] = config.LeaseSigningPublicKeyPem,
            ["UpdateChannel"] = config.UpdateChannel,
            ["UpdatePackageSigningPublicKeyPem"] = config.UpdatePackageSigningPublicKeyPem,
            ["PlayerShellExecutablePath"] = Path.Combine(programFiles, @"AFK4\Player Shell\AFK4.Player.Shell.exe"),
            ["PlayerShellAutoStartEnabled"] = bool.TrueString,
            ["OrganizationAdminExecutablePath"] = Path.Combine(programFiles, @"AFK4\Organization Admin\AFK4.OrganizationAdmin.App.exe"),
            ["OrganizationAdminUpdateCoordinationPipeName"] = organizationAdminPipeName,
            ["OrganizationAdminUpdateCoordinationSecret"] = organizationAdminCoordinationSecret,
            ["UpdateInstallerExecutablePath"] = powershell,
            ["UpdateInstallerArgumentsTemplate"] =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "install-afk4-update-msi.ps1")}\" -PackagePath \"{{PackagePath}}\" -Component \"{{Component}}\" -Version \"{{Version}}\"",
            ["UpdateRollbackExecutablePath"] = powershell,
            ["UpdateRollbackArgumentsTemplate"] =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "rollback-afk4-update-msi.ps1")}\" -PackagePath \"{{ArtifactPath}}\" -Component \"{{Component}}\" -Version \"{{TargetVersion}}\"",
            ["UpdateRestartExecutablePath"] = powershell,
            ["UpdateRestartArgumentsTemplate"] =
                $"-NoProfile -ExecutionPolicy Bypass -File \"{Path.Combine(helperDirectory, "restart-afk4-agent-service.ps1")}\"",
        };
    }

    private static string DeriveOrganizationAdminCoordinationSecret(SetupWizardBootstrapConfig config)
    {
        var key = Encoding.UTF8.GetBytes(config.CredentialSecret);
        var context = Encoding.UTF8.GetBytes($"AFK4.OrganizationAdmin.UpdateCoordination/v1/{config.DeviceId:D}");
        var secret = HMACSHA256.HashData(key, context);
        return Convert.ToBase64String(secret).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string ProgramFilesPath()
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return string.IsNullOrWhiteSpace(programFiles) ? @"C:\Program Files" : programFiles;
    }
}
