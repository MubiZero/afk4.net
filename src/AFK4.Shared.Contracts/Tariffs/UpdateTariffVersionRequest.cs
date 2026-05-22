namespace AFK4.Shared.Contracts.Tariffs;

public sealed record UpdateTariffVersionRequest(
    Guid OrganizationId,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    bool IsActive);
