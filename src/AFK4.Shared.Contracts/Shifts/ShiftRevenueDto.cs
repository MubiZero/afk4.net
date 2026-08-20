using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

/// <summary>
/// Чем смена заработала: проданным временем, товаром и удержанной за неявку предоплатой.
///
/// <paramref name="NoShow"/> стоит отдельно от <paramref name="Time"/> намеренно: удержание — это
/// не проданное время, и сложить их значит показать кассе наигранные часы, которых не было.
/// В <paramref name="Total"/> оно входит — это заработанные деньги, и потерять их в отчёте нельзя.
/// </summary>
public sealed record EarnedBreakdownDto(MoneyDto Time, MoneyDto Goods, MoneyDto NoShow, MoneyDto Total);

public sealed record InflowBreakdownDto(MoneyDto Cash, MoneyDto NonCash, MoneyDto WalletTopUps, MoneyDto DirectTotal);

public sealed record CashReconciliationDto(MoneyDto Starting, MoneyDto Expected, MoneyDto? Counted, MoneyDto? Difference);

public sealed record ShiftRevenueDto(
    Guid ShiftId,
    Guid OrganizationId,
    Guid BranchId,
    Guid OpenedByStaffUserId,
    Guid? ClosedByStaffUserId,
    string State,
    EarnedBreakdownDto Earned,
    InflowBreakdownDto Inflow,
    CashReconciliationDto Cash,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc);

public sealed record ShiftRevenueListDto(IReadOnlyList<ShiftRevenueDto> Shifts, int Limit);
