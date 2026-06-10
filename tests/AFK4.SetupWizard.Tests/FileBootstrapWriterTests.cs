using System.Text.Json;
using AFK4.SetupWizard.Core;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Tests;

public sealed class FileBootstrapWriterTests
{
    private static SetupWizardBootstrapConfig SampleConfig() => new(
        OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
        BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
        DeviceId: Guid.Parse("11111111-1111-4111-8111-111111111111"),
        CredentialId: Guid.Parse("22222222-2222-4222-8222-222222222222"),
        CredentialSecret: "afk4_secret_value",
        Role: DeviceRoleNames.GamingPc,
        ApiBaseUrl: "https://afk4.staging.mubi.dev/",
        UpdateChannel: "internal",
        LeaseSigningPublicKeyPem: "lease-pem",
        UpdatePackageSigningPublicKeyPem: "update-pem");

    [Fact]
    public void Write_ProducesJsonThatBindsTheAgentSectionTheServiceReads()
    {
        var path = Path.Combine(Path.GetTempPath(), $"afk4-bootstrap-{Guid.NewGuid():N}.json");
        try
        {
            new FileBootstrapWriter("DESKTOP-TEST", path, restrictAccess: false).Write(SampleConfig());

            // Structure mirrors what Program.cs reads: AddJsonFile(...).GetSection("Agent").
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var agent = document.RootElement.GetProperty("Agent");

            Assert.Equal("afk4_secret_value", agent.GetProperty("DeviceCredentialSecret").GetString());
            Assert.Equal("https://afk4.staging.mubi.dev", agent.GetProperty("PlatformBaseUrl").GetString()); // trailing slash trimmed
            Assert.Equal(DeviceRoleNames.GamingPc, agent.GetProperty("DeviceRole").GetString());
            Assert.Equal("DESKTOP-TEST", agent.GetProperty("MachineName").GetString());
            // Emitted as a real JSON boolean so AgentOptions.PlayerShellAutoStartEnabled binds.
            Assert.Equal(JsonValueKind.True, agent.GetProperty("PlayerShellAutoStartEnabled").ValueKind);
            Assert.EndsWith(@"AFK4\Player Shell\AFK4.Player.Shell.exe", agent.GetProperty("PlayerShellExecutablePath").GetString());
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Write_CreatesMissingParentDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"afk4-bootstrap-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "nested", "bootstrap.json");
        try
        {
            new FileBootstrapWriter("DESKTOP-TEST", path, restrictAccess: false).Write(SampleConfig());

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
