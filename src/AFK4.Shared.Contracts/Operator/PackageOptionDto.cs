namespace AFK4.Shared.Contracts.Operator;

public sealed record PackageOptionDto(
    Guid PackageDefinitionId,
    string Name,
    string CurrencyCode,
    long PriceMinorUnits,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays);
