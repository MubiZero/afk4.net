namespace AFK4.Shared.Contracts.Identity;

/// <summary>
/// Приём приглашения: номер, код первого входа от руководителя (SMS его только дублирует) и ПИН,
/// который человек придумывает себе сам.
/// </summary>
public sealed record AcceptStaffInviteRequest(string PhoneNumber, string Code, string Password);

/// <summary>
/// Кем человек стал — клуб и логин — и сразу вход: придумав ПИН, он не вводит его второй раз.
/// </summary>
public sealed record AcceptStaffInviteResponse(Guid OrganizationId, string UserName, StaffSignInResponse SignIn);
