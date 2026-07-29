using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class EnvironmentBootstrapWriterTests : IDisposable
{
    private static readonly string[] Keys =
    [
        "Agent__PlatformBaseUrl",
        "Agent__OrganizationId",
        "Agent__BranchId",
        "Agent__DeviceId",
        "Agent__MachineName",
        "Agent__DeviceRole",
        "Agent__DeviceCredentialSecret",
        "Agent__LeaseSigningPublicKeyPem",
        "Agent__UpdateChannel",
        "Agent__PlayerShellExecutablePath",
        "Agent__PlayerShellAutoStartEnabled",
        "Agent__UpdateInstallerExecutablePath",
        "Agent__UpdateInstallerArgumentsTemplate",
        "Agent__UpdateRollbackExecutablePath",
        "Agent__UpdateRollbackArgumentsTemplate",
        "Agent__UpdateRestartExecutablePath",
        "Agent__UpdateRestartArgumentsTemplate",
        "Agent__UpdatePackageSigningPublicKeyPem",
        "Agent__OrganizationAdminExecutablePath",
        "Agent__OrganizationAdminUpdateCoordinationPipeName",
        "Agent__OrganizationAdminUpdateCoordinationSecret",
        "AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL",
        "AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID",
        "AFK4_ORGANIZATION_ADMIN_BRANCH_ID",
        "AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME",
        "AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"
    ];

    [WindowsOnlyFact]
    public void Write_StoresAgentBootstrapEnvironmentValues()
    {
        Clear();
        var config = new SetupWizardBootstrapConfig(
            Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            Guid.Parse("11111111-1111-4111-8111-111111111111"),
            Guid.Parse("22222222-2222-4222-8222-222222222222"),
            "credential-secret",
            DeviceRoleNames.ManagerWorkstation,
            "https://afk4.staging.mubi.dev/",
            "internal",
            "lease-public-key",
            "update-public-key");

        new EnvironmentBootstrapWriter("MANAGER-01", EnvironmentVariableTarget.Process).Write(config);

        Assert.Equal("https://afk4.staging.mubi.dev", Environment.GetEnvironmentVariable("Agent__PlatformBaseUrl"));
        Assert.Equal(
            "https://afk4.staging.mubi.dev",
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL"));
        Assert.Equal(
            config.OrganizationId.ToString("D"),
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID"));
        Assert.Equal(
            config.BranchId.ToString("D"),
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_BRANCH_ID"));
        Assert.Equal(config.OrganizationId.ToString("D"), Environment.GetEnvironmentVariable("Agent__OrganizationId"));
        Assert.Equal(config.BranchId.ToString("D"), Environment.GetEnvironmentVariable("Agent__BranchId"));
        Assert.Equal(config.DeviceId.ToString("D"), Environment.GetEnvironmentVariable("Agent__DeviceId"));
        Assert.Equal("MANAGER-01", Environment.GetEnvironmentVariable("Agent__MachineName"));
        Assert.Equal(DeviceRoleNames.ManagerWorkstation, Environment.GetEnvironmentVariable("Agent__DeviceRole"));
        Assert.Equal("credential-secret", Environment.GetEnvironmentVariable("Agent__DeviceCredentialSecret"));
        Assert.Equal("lease-public-key", Environment.GetEnvironmentVariable("Agent__LeaseSigningPublicKeyPem"));
        Assert.Equal("internal", Environment.GetEnvironmentVariable("Agent__UpdateChannel"));
        Assert.EndsWith(
            @"WindowsPowerShell\v1.0\powershell.exe",
            Environment.GetEnvironmentVariable("Agent__UpdateInstallerExecutablePath"));
        Assert.Contains(
            @"AFK4\Update Helpers\install-afk4-update-msi.ps1",
            Environment.GetEnvironmentVariable("Agent__UpdateInstallerArgumentsTemplate"),
            StringComparison.Ordinal);
        Assert.EndsWith(
            @"WindowsPowerShell\v1.0\powershell.exe",
            Environment.GetEnvironmentVariable("Agent__UpdateRollbackExecutablePath"));
        Assert.Contains(
            @"AFK4\Update Helpers\rollback-afk4-update-msi.ps1",
            Environment.GetEnvironmentVariable("Agent__UpdateRollbackArgumentsTemplate"),
            StringComparison.Ordinal);
        Assert.EndsWith(
            @"WindowsPowerShell\v1.0\powershell.exe",
            Environment.GetEnvironmentVariable("Agent__UpdateRestartExecutablePath"));
        Assert.Contains(
            @"AFK4\Update Helpers\restart-afk4-agent-service.ps1",
            Environment.GetEnvironmentVariable("Agent__UpdateRestartArgumentsTemplate"),
            StringComparison.Ordinal);
        Assert.EndsWith(
            @"AFK4\Player Shell\AFK4.Player.Shell.exe",
            Environment.GetEnvironmentVariable("Agent__PlayerShellExecutablePath"));
        Assert.Equal(bool.TrueString, Environment.GetEnvironmentVariable("Agent__PlayerShellAutoStartEnabled"));
        Assert.Equal("update-public-key", Environment.GetEnvironmentVariable("Agent__UpdatePackageSigningPublicKeyPem"));
        Assert.EndsWith(@"AFK4\Organization Admin\AFK4.OrganizationAdmin.App.exe", Environment.GetEnvironmentVariable("Agent__OrganizationAdminExecutablePath"));
        Assert.Equal(
            Environment.GetEnvironmentVariable("Agent__OrganizationAdminUpdateCoordinationPipeName"),
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME"));
        Assert.Equal(
            Environment.GetEnvironmentVariable("Agent__OrganizationAdminUpdateCoordinationSecret"),
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"));
        Assert.NotEqual("credential-secret", Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"));
    }

    public void Dispose()
    {
        Clear();
    }

    private static void Clear()
    {
        foreach (var key in Keys)
        {
            Environment.SetEnvironmentVariable(key, null, EnvironmentVariableTarget.Process);
        }
    }
}
