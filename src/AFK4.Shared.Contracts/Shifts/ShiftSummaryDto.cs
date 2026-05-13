using AFK4.Shared.Contracts.Billing;

namespace AFK4.Shared.Contracts.Shifts;

public sealed record ShiftSummaryDto(
    Guid ShiftId,
    MoneyDto StartingCash,
    MoneyDto CashMovementsTotal,
    MoneyDto PosCashPaymentsTotal,
    MoneyDto PosRefundsTotal,
    MoneyDto ExpectedCash,
    MoneyDto CountedCash,
    MoneyDto Difference);
