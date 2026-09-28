using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Platform.Api.Shifts;

/// <summary>
/// Сколько наличных должно лежать в ящике смены. Одно правило на закрытие смены и на оба отчёта:
/// раньше оно было написано трижды, и расхождение, которое кассир видел при закрытии, отчёт мог
/// посчитать по-своему.
/// </summary>
public static class ShiftExpectedCash
{
    public static ShiftCashBreakdown Compute(
        ShiftEntity shift,
        IEnumerable<CashMovementEntity> cashMovements,
        IEnumerable<PaymentEntity> payments,
        IEnumerable<LedgerEntryEntity> ledgerEntries)
    {
        bool InShift(Guid shiftId, string currencyCode) =>
            shiftId == shift.ShiftId &&
            string.Equals(currencyCode.Trim(), shift.CurrencyCode.Trim(), StringComparison.OrdinalIgnoreCase);

        var cashMovementTotal = cashMovements
            .Where(movement => InShift(movement.ShiftId, movement.CurrencyCode))
            .Sum(movement => movement.MovementType == CashMovementTypeNames.CashIn
                ? movement.AmountMinorUnits
                : -movement.AmountMinorUnits);
        // Возврат хранится с отрицательной суммой, поэтому оплаты и возвраты складываются как есть.
        var posCash = payments
            .Where(payment => InShift(payment.ShiftId, payment.CurrencyCode) &&
                              payment.PaymentMethod == PaymentMethodNames.Cash)
            .ToList();
        var posCashRefunds = posCash
            .Where(payment => payment.PaymentKind == RefundPaymentKind)
            .Sum(payment => payment.AmountMinorUnits);
        var posCashPayments = posCash.Sum(payment => payment.AmountMinorUnits) - posCashRefunds;
        // Пополнение и ручная правка кладут наличные в ящик, погашение долга — тоже, но в журнале
        // оно записано со знаком списания с игрока.
        var billingCashTotal = ledgerEntries
            .Where(entry => entry.ShiftId is { } shiftId &&
                            InShift(shiftId, entry.CurrencyCode) &&
                            (entry.EntryType == LedgerEntryTypeNames.TopUp ||
                             entry.EntryType == LedgerEntryTypeNames.DebtPayment ||
                             entry.EntryType == LedgerEntryTypeNames.ManualCorrection))
            .Sum(entry => entry.EntryType == LedgerEntryTypeNames.DebtPayment
                ? -entry.AmountMinorUnits
                : entry.AmountMinorUnits);

        return new ShiftCashBreakdown(
            cashMovementTotal,
            posCashPayments,
            posCashRefunds,
            billingCashTotal,
            shift.StartingCashMinorUnits + cashMovementTotal + posCashPayments + posCashRefunds + billingCashTotal);
    }

    private const string RefundPaymentKind = "refund";
}

/// <summary>Из чего сложилась ожидаемая наличность смены, в копейках валюты смены.</summary>
public sealed record ShiftCashBreakdown(
    long CashMovements,
    long PosCashPayments,
    long PosCashRefunds,
    long BillingCash,
    long Expected);
