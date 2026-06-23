namespace AFK4.Shared.Contracts.Players;

public sealed record UpdatePlayerAccountRequest(
    Guid OrganizationId,
    string DisplayName,
    string? PhoneNumber);
