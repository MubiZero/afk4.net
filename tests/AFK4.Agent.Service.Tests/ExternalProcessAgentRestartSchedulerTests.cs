using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class ExternalProcessAgentRestartSchedulerTests
{
    [Theory]
    [InlineData(UpdateComponentNames.AgentService)]
    [InlineData(UpdateComponentNames.PlayerShell)]
    [InlineData(UpdateComponentNames.OrganizationAdmin)]
    public async Task ScheduleRestartAsync_RestartsAgentAfterComponentsThatWriteAgentEnvironment(string component)
    {
        var scheduler = new ExternalProcessAgentRestartScheduler(Options.Create(new AgentOptions
        {
            UpdateRestartExecutablePath = PowerShellPath(),
            UpdateRestartArgumentsTemplate = "-NoProfile -Command \"exit 0\""
        }));
        var state = CreateState(component);

        var result = await scheduler.ScheduleRestartAsync(state.Instruction, state.InstallState, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.True(result.IsRequired);
        Assert.Equal("Agent restart scheduled.", result.Message);
    }

    private static (ComponentUpdateInstructionDto Instruction, UpdateInstallState InstallState) CreateState(string component)
    {
        var instruction = new ComponentUpdateInstructionDto(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: component,
            Version: "1.2.3",
            Channel: UpdateChannelNames.Internal,
            ArtifactUri: "https://updates.afk4.test/package.msi",
            Sha256: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
            Signature: "base64-signature",
            SignatureAlgorithm: "ed25519",
            SizeBytes: 123,
            ReleaseNotes: "update");
        var artifact = new DownloadedUpdateArtifact(
            instruction,
            Path.Combine(Path.GetTempPath(), $"{instruction.UpdatePackageId:D}.msi"),
            instruction.SizeBytes);

        return (instruction, UpdateInstallState.Create(
            instruction,
            artifact,
            UpdateStatusNames.Installed,
            "installed",
            DateTimeOffset.Parse("2026-05-25T12:00:00Z")));
    }

    private static string PowerShellPath()
    {
        return Path.Combine(
            Environment.SystemDirectory,
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
    }
}
