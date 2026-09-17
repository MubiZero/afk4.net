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

    /// <summary>
    /// Пакет последней версии компонента, которая реально встала. Ради него всё и затевалось:
    /// без него «откат» сводился к переустановке того же сломанного пакета.
    /// </summary>
    Task<LastKnownGoodUpdate?> LoadLastKnownGoodAsync(string component, CancellationToken cancellationToken);

    Task SaveLastKnownGoodAsync(LastKnownGoodUpdate lastKnownGood, CancellationToken cancellationToken);
}
