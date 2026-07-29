using System.Text.Json;
using AFK4.Agent.Service.Shell;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public interface IOrganizationAdminProcessLauncher
{
    Task ScheduleAfterRestartAsync(ComponentUpdateInstructionDto instruction, CancellationToken cancellationToken);
    Task<bool> LaunchPendingAsync(CancellationToken cancellationToken);
}

public sealed class OrganizationAdminProcessLauncher(
    IOptions<AgentOptions> options,
    IPlayerShellLaunchContext launchContext,
    IPlayerShellProcessStarter processStarter,
    IPlayerShellProcessQuery processQuery) : IOrganizationAdminProcessLauncher
{
    private sealed record PendingLaunch(Guid UpdateRolloutId, Guid UpdatePackageId, string Version);

    public async Task ScheduleAfterRestartAsync(ComponentUpdateInstructionDto instruction, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Value.UpdateStateDirectory);
        var path = PendingPath();
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(
                new PendingLaunch(instruction.UpdateRolloutId, instruction.UpdatePackageId, instruction.Version)), cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    public Task<bool> LaunchPendingAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var pendingPath = PendingPath();
        if (!File.Exists(pendingPath)) return Task.FromResult(false);

        var executablePath = options.Value.OrganizationAdminExecutablePath;
        var target = launchContext.GetActiveUserSession();
        if (target is null || string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            return Task.FromResult(false);
        if (processQuery.IsRunning(executablePath, target.SessionId))
        {
            File.Delete(pendingPath);
            return Task.FromResult(true);
        }

        var claimedPath = $"{pendingPath}.claimed";
        File.Move(pendingPath, claimedPath, overwrite: true);
        try
        {
            processStarter.Start(executablePath, options.Value.OrganizationAdminStartArguments, target);
            File.Delete(claimedPath);
            return Task.FromResult(true);
        }
        catch
        {
            File.Move(claimedPath, pendingPath, overwrite: true);
            throw;
        }
    }

    private string PendingPath() => Path.Combine(options.Value.UpdateStateDirectory, "organization-admin-relaunch.json");
}
