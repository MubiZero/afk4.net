namespace AFK4.Shared.Contracts.Tariffs;

/// <summary>
/// Расписание: <c>AppliesOnDaysMask</c> — биты дней недели с понедельника (1) по воскресенье (64),
/// <c>0</c> означает «каждый день». Часы — минуты от полуночи по местному времени филиала; оба
/// <c>null</c> означают «круглые сутки», а начало больше конца — окно через полночь.
/// </summary>
public sealed record TariffDto(
    Guid TariffId,
    Guid OrganizationId,
    Guid BranchId,
    string Name,
    bool IsActive,
    DateTimeOffset CreatedAtUtc,
    int AppliesOnDaysMask = 0,
    int? AppliesFromMinuteOfDay = null,
    int? AppliesToMinuteOfDay = null);
