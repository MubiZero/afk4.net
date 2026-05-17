using AFK4.Shared.Contracts.Devices;

namespace AFK4.GamingPc.Setup.Core;

public sealed class AgentMachineConfigurationWriter : IAgentMachineConfigurationWriter
{
    public void Write(SetupRequest request, DeviceEnrollmentResponse enrollment)
    {
        Write("Agent__PlatformBaseUrl", StagingSetupDefaults.PlatformBaseUrl.ToString().TrimEnd('/'));
        Write("Agent__OrganizationId", StagingSetupDefaults.OrganizationId.ToString("D"));
        Write("Agent__BranchId", StagingSetupDefaults.BranchId.ToString("D"));
        Write("Agent__DeviceId", enrollment.DeviceId.ToString("D"));
        Write("Agent__MachineName", request.MachineName);
        Write("Agent__AgentVersion", StagingSetupDefaults.AgentVersion);
        Write("Agent__ShellVersion", StagingSetupDefaults.ShellVersion);
        Write("Agent__DeviceCredentialSecret", enrollment.CredentialSecret);
        Write("Agent__LeaseSigningPublicKeyPem", request.LeaseSigningPublicKeyPem);
        Write("Agent__PlayerShellExecutablePath", StagingSetupDefaults.PlayerShellExecutablePath);
        Write("Agent__UpdateChannel", StagingSetupDefaults.UpdateChannel);
        Write("Agent__UpdateInstallerExecutablePath", StagingSetupDefaults.UpdateInstallerExecutablePath);
        Write("Agent__UpdateInstallerArgumentsTemplate", StagingSetupDefaults.UpdateInstallerArgumentsTemplate);
        Write("Agent__UpdateRollbackExecutablePath", StagingSetupDefaults.UpdateRollbackExecutablePath);
        Write("Agent__UpdateRollbackArgumentsTemplate", StagingSetupDefaults.UpdateRollbackArgumentsTemplate);
        Write("Agent__UpdateRestartExecutablePath", StagingSetupDefaults.UpdateRestartExecutablePath);
        Write("Agent__UpdateRestartArgumentsTemplate", StagingSetupDefaults.UpdateRestartArgumentsTemplate);
    }

    private static void Write(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Machine);
    }
}
