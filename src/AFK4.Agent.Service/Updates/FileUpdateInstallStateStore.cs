using System.Text.Json;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Updates;

public sealed class FileUpdateInstallStateStore(IOptions<AgentOptions> options) : IUpdateInstallStateStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<UpdateInstallState> BeginInstallAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken)
    {
        var state = UpdateInstallState.Create(
            instruction,
            artifact,
            UpdateStatusNames.Installing,
            "Update installation started.",
            observedAtUtc);

        await SaveAsync(state, cancellationToken);

        return state;
    }

    public async Task SaveAsync(UpdateInstallState state, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Value.UpdateStateDirectory);
        var path = GetStatePath(state.UpdateRolloutId, state.UpdatePackageId);
        var tempPath = $"{path}.tmp";
        var json = JsonSerializer.Serialize(state, SerializerOptions);

        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Copy(tempPath, path, overwrite: true);
        File.Delete(tempPath);
    }

    public async Task<IReadOnlyList<UpdateInstallState>> LoadRecoverableAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(options.Value.UpdateStateDirectory))
        {
            return [];
        }

        var states = new List<UpdateInstallState>();
        foreach (var path in Directory.EnumerateFiles(options.Value.UpdateStateDirectory, "*.json"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            var state = JsonSerializer.Deserialize<UpdateInstallState>(json, SerializerOptions);
            if (state is not null && IsRecoverable(state.Status))
            {
                states.Add(state);
            }
        }

        return states;
    }

    private string GetStatePath(Guid rolloutId, Guid packageId)
    {
        return Path.Combine(
            options.Value.UpdateStateDirectory,
            $"{rolloutId:N}-{packageId:N}.json");
    }

    private static bool IsRecoverable(string status)
    {
        return status is UpdateStatusNames.Installing or UpdateStatusNames.RollbackStarted;
    }
}
