using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCredentialLifecycleService
{
    /// <summary>
    /// Отрезать машину немедленно и выдать новый ключ человеку. Старые ключи отзываются в ту же
    /// секунду, поэтому агент на этой машине сразу теряет доступ: это путь для украденного или
    /// скомпрометированного ПК, где так и надо.
    /// </summary>
    Task<RotateDeviceCredentialResponse?> RotateAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Попросить машину перевыпустить себе ключ самой. Ничего не отзывает: просьба уезжает агенту
    /// ближайшим сердцебиением, и меняет ключ он сам. Это путь гигиены — ключ обновляется без
    /// визита к ПК и без простоя.
    /// </summary>
    Task<bool> RequestRotationAsync(Guid deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Агент меняет свой ключ сам. Старый остаётся принятым ещё <c>overlap</c> — на случай, если
    /// ПК выключится между ответом сервера и записью нового ключа на диск.
    /// </summary>
    Task<RotateDeviceCredentialResponse?> RotateForAgentAsync(Guid deviceId, CancellationToken cancellationToken);

    Task<RevokeDeviceCredentialResponse?> RevokeAsync(Guid deviceId, Guid credentialId, CancellationToken cancellationToken);
}
