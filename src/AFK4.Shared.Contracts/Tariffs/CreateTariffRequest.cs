namespace AFK4.Shared.Contracts.Tariffs;

public sealed record CreateTariffRequest(
    Guid OrganizationId,
    string Name,
    string IdempotencyKey);
