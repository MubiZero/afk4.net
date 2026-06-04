namespace AFK4.Shared.Contracts.Players;

public sealed record PlayerSelfStartRequest(
    Guid DeviceId,
    string TariffRuleVersionId,
    int DurationMinutes,
    string IdempotencyKey);
