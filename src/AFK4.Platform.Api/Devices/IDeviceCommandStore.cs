using AFK4.Shared.Contracts.Devices;

namespace AFK4.Platform.Api.Devices;

public interface IDeviceCommandStore
{
    /// <summary>
    /// Записывает команду в очередь ПК. С ключом повтора, который уже занят (два одинаковых
    /// запроса пришли разом), возвращает ту, что записалась первой, — её и нужно отдавать.
    /// </summary>
    Task<DeviceCommandDto> AddPendingAsync(
        Guid deviceId,
        DeviceCommandDto command,
        string? idempotencyKey,
        CancellationToken cancellationToken);

    Task<DeviceCommandDto?> FindByIdempotencyKeyAsync(string idempotencyKey, CancellationToken cancellationToken);

    Task ApplyResultAsync(DeviceCommandResultDto result, CancellationToken cancellationToken);

    Task<DeviceCommandStatusDto?> GetAsync(Guid deviceId, Guid commandId, CancellationToken cancellationToken);
}
