namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Приём приглашения: номер, код из SMS и пароль, который человек придумывает себе сам.
/// </summary>
public sealed record AcceptStaffInviteRequest(string PhoneNumber, string Code, string Password);

/// <summary>Кем человек стал: клуб и его логин в нём.</summary>
public sealed record AcceptStaffInviteResponse(Guid OrganizationId, string UserName);
