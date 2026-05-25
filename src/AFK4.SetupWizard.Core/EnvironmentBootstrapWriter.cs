using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

public sealed class EnvironmentBootstrapWriter(
    string machineName,
    EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) : ISetupWizardBootstrapWriter
{
    public void Write(SetupWizardBootstrapConfig config)
    {
        Write("Agent__PlatformBaseUrl", config.ApiBaseUrl.TrimEnd('/'));
        Write("Agent__OrganizationId", config.OrganizationId.ToString("D"));
        Write("Agent__BranchId", config.BranchId.ToString("D"));
        Write("Agent__DeviceId", config.DeviceId.ToString("D"));
        Write("Agent__MachineName", machineName);
        Write("Agent__DeviceRole", string.IsNullOrWhiteSpace(config.Role) ? DeviceRoleNames.GamingPc : config.Role);
        Write("Agent__DeviceCredentialSecret", config.CredentialSecret);
        Write("Agent__LeaseSigningPublicKeyPem", config.LeaseSigningPublicKeyPem);
        Write("Agent__UpdateChannel", config.UpdateChannel);
        Write("Agent__UpdatePackageSigningPublicKeyPem", config.UpdatePackageSigningPublicKeyPem);
    }

    private void Write(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }
}
