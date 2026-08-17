namespace AFK4.Shared.Contracts.Tariffs;

public sealed record UpdateTariffRequest(
    Guid OrganizationId,
    string Name,
    bool IsActive,
    int AppliesOnDaysMask = 0,
    int? AppliesFromMinuteOfDay = null,
    int? AppliesToMinuteOfDay = null);
