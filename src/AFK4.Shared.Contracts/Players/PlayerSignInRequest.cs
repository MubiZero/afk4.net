namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSignInRequest(
    Guid OrganizationId,
    string PhoneNumber,
    string Password);
