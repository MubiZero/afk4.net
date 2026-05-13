namespace AFK4.Shared.Contracts.Tariffs;

public sealed record TariffDto(
    Guid TariffId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc);
