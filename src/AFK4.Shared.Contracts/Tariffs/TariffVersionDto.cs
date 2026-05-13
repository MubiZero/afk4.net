namespace AFK4.Shared.Contracts.Tariffs;

public sealed record TariffVersionDto(
    Guid TariffVersionId,
    Guid TariffId,
    int VersionNumber,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? RetiredAtUtc,
    DateTimeOffset CreatedAtUtc);
