namespace AFK4.Shared.Contracts.Tariffs;

public sealed record UpdateTariffRequest(
    Guid OrganizationId,
    string Name,
    bool IsActive);
