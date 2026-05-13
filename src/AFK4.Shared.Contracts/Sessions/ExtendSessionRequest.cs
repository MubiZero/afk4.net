namespace AFK4.Shared.Contracts.Sessions;

public sealed record ExtendSessionRequest(
    int AdditionalMinutes,
    string TariffRuleVersionId,
    string IdempotencyKey,
    Guid? PlayerAccountId = null,
    string BillingMode = "",
    Guid? TariffVersionId = null,
    Guid? PlayerPackageId = null);
