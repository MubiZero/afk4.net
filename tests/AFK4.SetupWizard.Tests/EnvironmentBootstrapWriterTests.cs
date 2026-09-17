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
            "https://api.afk4.net/",
            "internal",
            "lease-public-key",
            "update-public-key");

        new EnvironmentBootstrapWriter("MANAGER-01", EnvironmentVariableTarget.Process).Write(config);

        Assert.Equal("https://api.afk4.net", Environment.GetEnvironmentVariable("Agent__PlatformBaseUrl"));
        Assert.Equal(
            "https://api.afk4.net",
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
        Assert.NotEqual("credential-secret", Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"));
    }

    // Машинные переменные читает любая учётная запись на этой машине. Ключ, которым ПК
    // представляется платформе, туда попадать не должен: в bootstrap.json он заперт правами, а
    // рядом лежал открытым текстом — гость за игровым ПК читал его одной командой.
    [WindowsOnlyFact]
    public void Write_NeverPublishesTheDeviceCredential()
    {
        Clear();

        new EnvironmentBootstrapWriter("PC-01", EnvironmentVariableTarget.Process)
            .Write(CreateConfig(DeviceRoleNames.GamingPc));

        Assert.Null(Environment.GetEnvironmentVariable("Agent__DeviceCredentialSecret"));
        Assert.Null(Environment.GetEnvironmentVariable("Agent__OrganizationAdminUpdateCoordinationSecret"));
        // Всё остальное на месте: запасной канал настройки остался рабочим.
        Assert.Equal("PC-01", Environment.GetEnvironmentVariable("Agent__MachineName"));
        Assert.Equal("lease-public-key", Environment.GetEnvironmentVariable("Agent__LeaseSigningPublicKeyPem"));
    }

    // На игровом ПК приложения клуба нет вовсе — и его настроек, включая секрет согласования
    // обновлений, там быть не должно.
    [WindowsOnlyFact]
    public void Write_OnAGamingPc_LeavesOutTheClubApplicationSettings()
    {
        Clear();

        new EnvironmentBootstrapWriter("PC-01", EnvironmentVariableTarget.Process)
            .Write(CreateConfig(DeviceRoleNames.GamingPc));

        Assert.Null(Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL"));
        Assert.Null(Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"));
    }

    // На рабочем месте управляющего приложение работает от имени кассира и запертый файл
    // прочитать не может — секрет согласования обновлений остаётся в переменных среды.
    [WindowsOnlyFact]
    public void Write_OnAManagerWorkstation_KeepsTheClubApplicationSettings()
    {
        Clear();

        new EnvironmentBootstrapWriter("MANAGER-01", EnvironmentVariableTarget.Process)
            .Write(CreateConfig(DeviceRoleNames.ManagerWorkstation));

        Assert.False(string.IsNullOrWhiteSpace(
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET")));
        Assert.Equal(
            "https://api.afk4.net",
            Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL"));
    }

    // Машина могла быть настроена прошлой версией мастера или в другой роли: секрет уже лежит в
    // переменных среды. Не написать его мало — надо убрать.
    [WindowsOnlyFact]
    public void Write_ScrubsSecretsLeftByAnEarlierSetup()
    {
        Clear();
        Environment.SetEnvironmentVariable("Agent__DeviceCredentialSecret", "old-secret", EnvironmentVariableTarget.Process);
        Environment.SetEnvironmentVariable(
            "AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET", "old-coordination", EnvironmentVariableTarget.Process);

        new EnvironmentBootstrapWriter("PC-01", EnvironmentVariableTarget.Process)
            .Write(CreateConfig(DeviceRoleNames.GamingPc));

        Assert.Null(Environment.GetEnvironmentVariable("Agent__DeviceCredentialSecret"));
        Assert.Null(Environment.GetEnvironmentVariable("AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET"));
    }

    private static SetupWizardBootstrapConfig CreateConfig(string role) => new(
        Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        "credential-secret",
        role,
        "https://api.afk4.net/",
        "internal",
        "lease-public-key",
        "update-public-key");

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
