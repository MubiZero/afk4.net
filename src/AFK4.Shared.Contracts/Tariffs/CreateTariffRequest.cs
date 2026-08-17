namespace AFK4.Shared.Contracts.Tariffs;

public sealed record CreateTariffRequest(
    Guid OrganizationId,
    string Name,
    string IdempotencyKey,
    int AppliesOnDaysMask = 0,
    int? AppliesFromMinuteOfDay = null,
    int? AppliesToMinuteOfDay = null);
