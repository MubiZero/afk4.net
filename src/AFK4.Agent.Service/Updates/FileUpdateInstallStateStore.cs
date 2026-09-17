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
        // Move, а не Copy: копирование не атомарно, и выключение питания ровно в этот момент
        // оставляло на диске обрезанный json — с него потом падало восстановление.
        File.Move(tempPath, path, overwrite: true);
    }

    public async Task<LastKnownGoodUpdate?> LoadLastKnownGoodAsync(string component, CancellationToken cancellationToken)
    {
        var path = GetLastKnownGoodPath(component);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(path, cancellationToken);
            return JsonSerializer.Deserialize<LastKnownGoodUpdate>(json, SerializerOptions);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // Нечитаемая запись — то же самое, что её отсутствие: откатываться будет некуда, и
            // установщик скажет об этом прямо, а не сделает вид, что откатился.
            return null;
        }
    }

    public async Task SaveLastKnownGoodAsync(LastKnownGoodUpdate lastKnownGood, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(options.Value.UpdateStateDirectory);
        var path = GetLastKnownGoodPath(lastKnownGood.Component);
        var tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(lastKnownGood, SerializerOptions), cancellationToken);
        File.Move(tempPath, path, overwrite: true);
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
            UpdateInstallState? state;
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                state = JsonSerializer.Deserialize<UpdateInstallState>(json, SerializerOptions);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                // Один повреждённый файл не должен уносить с собой всё восстановление: раньше
                // исключение отсюда навсегда останавливало обновления на этой машине.
                continue;
            }

            if (state is not null && IsRecoverable(state.Status))
            {
                states.Add(state);
            }
        }

        return states;
    }

    private string GetLastKnownGoodPath(string component)
    {
        var safeComponent = string.Concat(component.Select(symbol =>
            char.IsLetterOrDigit(symbol) || symbol is '-' or '_' ? symbol : '_'));
        return Path.Combine(options.Value.UpdateStateDirectory, $"last-known-good-{safeComponent}.json");
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
