using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Packages;

public sealed record UpdatePackageDefinitionRequest(
    Guid OrganizationId,
    string Name,
    MoneyDto Price,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays,
    bool IsActive);
