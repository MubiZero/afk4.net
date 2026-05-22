namespace AFK4.Shared.Contracts.Operator;

public sealed record TariffOptionDto(
    Guid TariffId,
    Guid TariffVersionId,
    string Name,
    string TariffRuleVersionId,
    int VersionNumber,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc);
