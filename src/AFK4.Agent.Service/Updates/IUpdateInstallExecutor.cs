using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IUpdateInstallExecutor
{
    Task<UpdateInstallResult> ExecuteAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        UpdateInstallState state,
        CancellationToken cancellationToken);
}
