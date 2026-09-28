using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Shifts;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Payments;
using AFK4.Shared.Contracts.Shifts;

namespace AFK4.Platform.Api.Tests.Shifts;

/// <summary>
/// Ожидаемую наличность раньше считали три копии: закрытие смены и два отчёта. Кассир видел одно
/// расхождение при закрытии, отчёт мог показать другое. Теперь правило одно — и здесь записано,
/// из чего оно складывается.
/// </summary>
public sealed class ShiftExpectedCashTests
{
    private static readonly Guid ShiftId = Guid.NewGuid();

    [Fact]
    public void Drawer_HoldsStartingCash_PlusEverythingThatWentThroughItInTheShiftCurrency()
    {
        var shift = new ShiftEntity { ShiftId = ShiftId, CurrencyCode = "TJS", StartingCashMinorUnits = 10_000 };

        var cash = ShiftExpectedCash.Compute(
            shift,
            [
                Movement(CashMovementTypeNames.CashIn, 500),
                Movement(CashMovementTypeNames.CashOut, 2_000),
            ],
            [
                Payment(PaymentMethodNames.Cash, "payment", 3_000),
                // Возврат хранится со знаком минус — ящик отдаёт деньги.
                Payment(PaymentMethodNames.Cash, "refund", -1_000),
                // Карта в ящик не попадает.
                Payment(PaymentMethodNames.CardManual, "payment", 9_999),
                // Другая смена — не наш ящик.
                Payment(PaymentMethodNames.Cash, "payment", 7_777, shiftId: Guid.NewGuid()),
            ],
            [
                Ledger(LedgerEntryTypeNames.TopUp, 4_000),
                // Погашение долга записано как списание с игрока, но наличные в ящик пришли.
                Ledger(LedgerEntryTypeNames.DebtPayment, -1_500),
                Ledger(LedgerEntryTypeNames.ManualCorrection, 200),
                // Игра со счёта — не наличные.
                Ledger(LedgerEntryTypeNames.GameplayCharge, -600),
                // Чужая валюта в ящик смены в сомони не кладётся.
                Ledger(LedgerEntryTypeNames.TopUp, 5_000, currencyCode: "USD"),
            ]);

        Assert.Equal(-1_500, cash.CashMovements);
        Assert.Equal(3_000, cash.PosCashPayments);
        Assert.Equal(-1_000, cash.PosCashRefunds);
        Assert.Equal(5_700, cash.BillingCash);
        Assert.Equal(10_000 - 1_500 + 3_000 - 1_000 + 5_700, cash.Expected);
    }

    private static CashMovementEntity Movement(string type, long amount) =>
        new() { ShiftId = ShiftId, MovementType = type, CurrencyCode = "TJS", AmountMinorUnits = amount };

    private static PaymentEntity Payment(string method, string kind, long amount, Guid? shiftId = null) =>
        new()
        {
            ShiftId = shiftId ?? ShiftId,
            PaymentMethod = method,
            PaymentKind = kind,
            CurrencyCode = "tjs",
            AmountMinorUnits = amount
        };

    private static LedgerEntryEntity Ledger(string type, long amount, string currencyCode = "TJS") =>
        new() { ShiftId = ShiftId, EntryType = type, CurrencyCode = currencyCode, AmountMinorUnits = amount };
}
