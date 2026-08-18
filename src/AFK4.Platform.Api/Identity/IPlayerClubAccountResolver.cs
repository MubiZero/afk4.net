using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Какой клуб человек имеет в виду прямо сейчас. Порядок один и тот же на всех маршрутах, и он же
/// — вся совместимость со старыми клиентами: приложение, которое про выбор клуба ничего не знает,
/// попадает в закреплённый в токене клуб и не замечает перемены.
/// </summary>
public interface IPlayerClubAccountResolver
{
    Task<PlayerAccountEntity?> ResolveAsync(
        Guid platformPersonId,
        Guid? requestedOrganizationId,
        Guid? pinnedOrganizationId,
        CancellationToken cancellationToken);
}
