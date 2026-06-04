namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSelfExtendRequest(
    int AdditionalMinutes,
    string IdempotencyKey);
