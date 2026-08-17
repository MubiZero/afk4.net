namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>
/// <c>Schedule</c> не передан — новый тариф действует круглосуточно и каждый день.
/// </summary>
public sealed record CreateTariffRequest(
    Guid OrganizationId,
    string Name,
    string IdempotencyKey,
    TariffScheduleDto? Schedule = null);
