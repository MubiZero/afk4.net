namespace AFK4.Shared.Contracts.Tariffs;

public sealed record CreateTariffVersionRequest(
    Guid OrganizationId,
    Guid TariffId,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    string IdempotencyKey);
