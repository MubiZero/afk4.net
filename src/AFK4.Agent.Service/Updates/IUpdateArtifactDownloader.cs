using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IUpdateArtifactDownloader
{
    Task<DownloadedUpdateArtifact> DownloadAsync(
        ComponentUpdateInstructionDto instruction,
        CancellationToken cancellationToken);
}
