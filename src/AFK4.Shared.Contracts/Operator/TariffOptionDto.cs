namespace AFK4.Shared.Contracts.Operator;

/// <summary>
/// Тариф, который можно выбрать. <c>AppliesNow</c> считает сервер по часовому поясу филиала:
/// клиент, повторивший этот расчёт у себя, ошибётся на телефоне с чужим часовым поясом и
/// предложит утреннюю цену вечером.
/// </summary>
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
    DateTimeOffset EffectiveFromUtc,
    int AppliesOnDaysMask = 0,
    int? AppliesFromMinuteOfDay = null,
    int? AppliesToMinuteOfDay = null,
    bool AppliesNow = true);
