namespace AFK4.Shared.Contracts.Packages;

/// <summary>Купленный пакет с остатком времени.</summary>
public sealed record PlayerPackageDto(
    Guid PlayerPackageId,
    Guid PackageDefinitionId,
    Guid PlayerAccountId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto PurchasedPrice,
    int IncludedSeconds,
    int BonusSeconds,
    int RemainingIncludedSeconds,
    int RemainingBonusSeconds,
    DateTimeOffset PurchasedAtUtc,
    DateTimeOffset? ExpiresAtUtc);
