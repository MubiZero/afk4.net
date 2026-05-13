namespace AFK4.Shared.Contracts.Tariffs;

public sealed record CalculateTariffRequest(
    Guid OrganizationId,
    Guid TariffVersionId,
    int DurationMinutes);
