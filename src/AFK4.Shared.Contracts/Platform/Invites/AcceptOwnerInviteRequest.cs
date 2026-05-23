namespace AFK4.Shared.Contracts.Platform.Invites;

public sealed record AcceptOwnerInviteRequest(
    string Code,
    string UserName,
    string DisplayName,
    string Password);
