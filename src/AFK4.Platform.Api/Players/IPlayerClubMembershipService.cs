using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Players;

/// <summary>
/// Связь человека с клубом и его счёт в этом клубе. Клуб человека не заводит — он получает доступ
/// к уже существующей личности в тот момент, когда она впервые что-то у него просит: бронь,
/// пополнение или посадку за ПК.
/// </summary>
public interface IPlayerClubMembershipService
{
    /// <summary>
    /// Возвращает счёт человека в клубе, открывая его при необходимости. Идемпотентно по самой
    /// природе задачи: у человека в клубе ровно один счёт, и это обещание держит уникальный
    /// индекс `(PlatformPersonId, OrganizationId)`, а не аккуратность вызывающего.
    /// <paramref name="branchId"/> можно не называть, если филиал у клуба один.
    /// </summary>
    Task<PlayerClubMembershipResult> EnsureAsync(
        Guid platformPersonId,
        Guid organizationId,
        Guid? branchId,
        CancellationToken cancellationToken);
}

public sealed record PlayerClubMembershipResult(PlayerAccountEntity? Account, bool Created, string? Error)
{
    public bool Succeeded => Account is not null;

    public static PlayerClubMembershipResult Opened(PlayerAccountEntity account) => new(account, true, null);

    public static PlayerClubMembershipResult Existing(PlayerAccountEntity account) => new(account, false, null);

    public static PlayerClubMembershipResult Refused(string error) => new(null, false, error);
}

public static class PlayerClubMembershipErrors
{
    public const string PersonNotFound = "person_not_found";
    public const string OrganizationNotFound = "organization_not_found";
    public const string BranchRequired = "branch_required";
    public const string BranchNotFound = "branch_not_found";

    /// <summary>
    /// Клуб закрыл человеку карточку. Оператору при деактивации обещано, что денежные операции и
    /// вход на место станут недоступны, — значит ни первое действие, ни PIN у ПК не имеют права
    /// открыть эту дверь заново.
    /// </summary>
    public const string ClubAccountClosed = "club_account_closed";
}
