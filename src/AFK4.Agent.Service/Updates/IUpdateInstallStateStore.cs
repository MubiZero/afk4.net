using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates;

public interface IUpdateInstallStateStore
{
    Task<UpdateInstallState> BeginInstallAsync(
        ComponentUpdateInstructionDto instruction,
        DownloadedUpdateArtifact artifact,
        DateTimeOffset observedAtUtc,
        CancellationToken cancellationToken);

    Task SaveAsync(UpdateInstallState state, CancellationToken cancellationToken);

    Task<IReadOnlyList<UpdateInstallState>> LoadRecoverableAsync(CancellationToken cancellationToken);
}
