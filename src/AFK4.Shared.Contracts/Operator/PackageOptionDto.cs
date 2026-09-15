namespace AFK4.Shared.Contracts.Operator;

/// <summary>
/// Пакет часов в прайсе клуба: предоплата, за которую час выходит дешевле поминутного тарифа.
/// </summary>
public sealed record PackageOptionDto(
    Guid PackageDefinitionId,
    string Name,
    string CurrencyCode,
    long PriceMinorUnits,
    // Оплаченное и бонусное время — две величины одного: игрок покупает часы, а не два
    // отдельных счётчика, и складывать их полагается тому, кто показывает.
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays);
