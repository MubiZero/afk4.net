using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Identity;

/// <summary>
/// Клуб этого запроса и счёт в нём. Клуб без счёта — не ошибка: так выглядит человек, впервые
/// заглянувший в незнакомый клуб, и именно из этого состояния вырастает первое действие.
/// </summary>
public sealed record PlayerClubSelection(Guid? OrganizationId, PlayerAccountEntity? Account)
{
    public static readonly PlayerClubSelection None = new(null, null);
}
