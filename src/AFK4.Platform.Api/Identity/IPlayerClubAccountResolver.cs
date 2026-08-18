using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Какой клуб человек имеет в виду прямо сейчас. Порядок один и тот же на всех маршрутах, и он же
/// — вся совместимость со старыми клиентами: приложение, которое про выбор клуба ничего не знает,
/// попадает в закреплённый в токене клуб и не замечает перемены.
/// </summary>
public interface IPlayerClubAccountResolver
{
    Task<PlayerClubSelection> ResolveAsync(
        Guid platformPersonId,
        Guid? requestedOrganizationId,
        Guid? pinnedOrganizationId,
        CancellationToken cancellationToken);
}

/// <summary>
/// Клуб этого запроса и счёт в нём. Клуб без счёта — не ошибка: так выглядит человек, впервые
/// заглянувший в незнакомый клуб, и именно из этого состояния вырастает первое действие.
/// </summary>
public sealed record PlayerClubSelection(Guid? OrganizationId, PlayerAccountEntity? Account)
{
    public static readonly PlayerClubSelection None = new(null, null);
}
