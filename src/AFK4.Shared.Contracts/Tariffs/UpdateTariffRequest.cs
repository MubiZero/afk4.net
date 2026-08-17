namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>
/// <c>Schedule</c> не передан — расписание остаётся прежним. Снятие тарифа с продажи и
/// переименование не должны требовать от вызывающего знания о часах.
/// </summary>
public sealed record UpdateTariffRequest(
    Guid OrganizationId,
    string Name,
    bool IsActive,
    TariffScheduleDto? Schedule = null);
