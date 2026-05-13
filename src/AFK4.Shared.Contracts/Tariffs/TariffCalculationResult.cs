namespace AFK4.Shared.Contracts.Tariffs;

public sealed record TariffCalculationResult(
    Guid TariffId,
    Guid TariffVersionId,
    string TariffRuleVersionId,
    int DurationMinutes,
    int BillableMinutes,
    AFK4.Shared.Contracts.Billing.MoneyDto Amount);
