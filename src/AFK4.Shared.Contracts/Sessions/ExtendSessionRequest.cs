namespace AFK4.Shared.Contracts.Sessions;

public sealed record ExtendSessionRequest(
    int AdditionalMinutes,
    string TariffRuleVersionId,
    string IdempotencyKey);
