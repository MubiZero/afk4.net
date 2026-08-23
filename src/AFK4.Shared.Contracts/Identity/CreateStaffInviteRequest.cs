namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Приглашение сотрудника по номеру телефона: человек принимает его коротким кодом из SMS и сам
/// задаёт себе пароль. Единственный путь завести сотрудника — заведение с готовым паролем убрано
/// намеренно, чтобы пароль знал только его владелец.
/// </summary>
/// <param name="Email">Необязательна: назвали — уйдёт и письмо, не назвали — хватит SMS.</param>
public sealed record CreateStaffInviteRequest(
    Guid OrganizationId,
    string UserName,
    string DisplayName,
    string PhoneNumber,
    string? Email,
    IReadOnlyList<string> RoleNames);
