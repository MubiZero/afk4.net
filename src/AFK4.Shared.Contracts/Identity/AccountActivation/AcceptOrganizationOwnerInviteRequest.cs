namespace AFK4.Shared.Contracts.Identity.AccountActivation;

public sealed record AcceptOrganizationOwnerInviteRequest(
    string Code,
    string UserName,
    string DisplayName,
    string Password);
