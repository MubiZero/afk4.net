using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class AgentBootstrapValuesTests
{
    [Fact]
    public void Build_ProvisionsOrganizationAdminExecutableAndAuthenticatedPipe()
    {
        var config = SampleConfig();

        var values = AgentBootstrapValues.Build(config, "MANAGER-01");

        Assert.EndsWith(
            @"AFK4\Organization Admin\AFK4.OrganizationAdmin.App.exe",
            values["OrganizationAdminExecutablePath"]);
        Assert.Equal(
            $"afk4-organization-admin-updates-{config.DeviceId:N}",
            values["OrganizationAdminUpdateCoordinationPipeName"]);
        Assert.NotEmpty(values["OrganizationAdminUpdateCoordinationSecret"]);
        Assert.NotEqual(config.CredentialSecret, values["OrganizationAdminUpdateCoordinationSecret"]);
        Assert.DoesNotContain(config.CredentialSecret, values["OrganizationAdminUpdateCoordinationSecret"], StringComparison.Ordinal);
    }

    [Fact]
    public void Build_DerivesStableDeviceBoundSecretWithoutExposingCredential()
    {
        var config = SampleConfig();

        var first = AgentBootstrapValues.Build(config, "MANAGER-01");
        var second = AgentBootstrapValues.Build(config, "MANAGER-01");
        var anotherDevice = config with { DeviceId = Guid.NewGuid() };

        Assert.Equal(first["OrganizationAdminUpdateCoordinationSecret"], second["OrganizationAdminUpdateCoordinationSecret"]);
        Assert.NotEqual(
            first["OrganizationAdminUpdateCoordinationSecret"],
            AgentBootstrapValues.Build(anotherDevice, "MANAGER-02")["OrganizationAdminUpdateCoordinationSecret"]);
    }

    private static SetupWizardBootstrapConfig SampleConfig() => new(
        Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        Guid.Parse("11111111-1111-4111-8111-111111111111"),
        Guid.Parse("22222222-2222-4222-8222-222222222222"),
        "credential-secret-that-must-not-leak",
        DeviceRoleNames.ManagerWorkstation,
        "https://afk4.staging.mubi.dev/",
        "internal",
        "lease-public-key",
        "update-public-key");
}
