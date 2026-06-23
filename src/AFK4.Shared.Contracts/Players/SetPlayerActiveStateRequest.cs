namespace AFK4.Shared.Contracts.Players;

public sealed record SetPlayerActiveStateRequest(
    Guid OrganizationId,
    bool IsActive);
