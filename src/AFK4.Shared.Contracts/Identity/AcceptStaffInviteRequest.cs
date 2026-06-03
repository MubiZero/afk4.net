namespace AFK4.Shared.Contracts.Identity;

public sealed record AcceptStaffInviteRequest(string Token, string Password);

/// <summary>Returned on a successful accept so the client can route the new staff member to sign-in.</summary>
public sealed record AcceptStaffInviteResponse(Guid OrganizationId, string UserName);
