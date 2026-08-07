using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Что считается выручкой филиала. Правило одно на весь проект: до этого оно жило только внутри
/// операторского дашборда, и второй его экземпляр в снимках разошёлся бы с первым молча — цифры
/// в двух местах перестали бы сходиться, и никакой тест этого не заметил бы.
/// </summary>
public static class BranchRevenue
{
    public const string DefaultCurrencyCode = "TJS";

    private const string PaymentKindPayment = "payment";
    private const string PaymentKindRefund = "refund";

    /// <summary>Нетто по кассе: платежи и возвраты (возврат приходит отрицательной суммой).</summary>
    public static long PosNet(IEnumerable<(string Kind, long AmountMinorUnits)> payments) =>
        payments.Sum(payment =>
            IsKind(payment.Kind, PaymentKindPayment) || IsKind(payment.Kind, PaymentKindRefund)
                ? payment.AmountMinorUnits
                : 0);

    /// <summary>Игровая выручка: списания за игру и возникший постоплатный долг минус возвраты.</summary>
    public static long Gameplay(IEnumerable<(string Kind, long AmountMinorUnits)> entries) =>
        entries.Sum(entry => entry.Kind switch
        {
            LedgerEntryTypeNames.GameplayCharge => Math.Abs(entry.AmountMinorUnits),
            LedgerEntryTypeNames.PostpaidDebt => Math.Max(0, entry.AmountMinorUnits),
            LedgerEntryTypeNames.Refund => -Math.Abs(entry.AmountMinorUnits),
            _ => 0
        });

    private static bool IsKind(string actual, string expected) =>
        string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
}
