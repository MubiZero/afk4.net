namespace AFK4.Shared.Contracts.Packages;

public sealed record CreatePackageDefinitionRequest(
    Guid OrganizationId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto Price,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays,
    string IdempotencyKey);
