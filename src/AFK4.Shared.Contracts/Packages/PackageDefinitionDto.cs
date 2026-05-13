namespace AFK4.Shared.Contracts.Packages;

public sealed record PackageDefinitionDto(
    Guid PackageDefinitionId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    AFK4.Shared.Contracts.Billing.MoneyDto Price,
    int IncludedSeconds,
    int BonusSeconds,
    int ExpiresAfterDays,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
