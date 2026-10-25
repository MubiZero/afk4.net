using AFK4.Shared.Contracts.Billing;

namespace AFK4.Platform.Api.Platform.Analytics;

/// <summary>
/// Что считается выручкой филиала. Правило одно на «Сводку» Панели, отчёт владельца и суточные снимки
/// платформы; деньги считаются по дню, когда они прошли (решение владельца 2026-09-23).
///
/// <list type="bullet">
/// <item><b>Игра</b> — журнал сессий: списание за игру (+), возникший постоплатный долг (+), возврат за игру (−).
/// Возврат пополнения кошелька (запись без сессии) выручкой не был и вычитаться не должен.</item>
/// <item><b>Товары</b> — чеки бара: сумма чека в день оплаты (+), в день возврата (−). Неоплаченный чек
/// (черновик, аннулирован) в выручку не входит.</item>
/// </list>
///
/// Платежи (<c>payments</c>) в правиле не участвуют. Раньше «Сводка» складывала все платежи и списания за
/// игру: наличная оплата гостевой сессии пишется и тем и другим, и игра считалась дважды. Отчёт владельца
/// брал только платежи с чеком и терял бар, оплаченный при расчёте сессии (такой платёж привязан к сессии,
/// а не к чеку). Платёж — это способ, которым пришли деньги, а не то, за что.
/// </summary>
public static class BranchRevenue
{
    public const string DefaultCurrencyCode = "TJS";

    /// <summary>Вклад одной записи журнала в игровую выручку.</summary>
    public static long GameplayImpact(string entryType, long amountMinorUnits, bool hasSession) => entryType switch
    {
        LedgerEntryTypeNames.GameplayCharge => Math.Abs(amountMinorUnits),
        // Погашение долга приходит той же записью с отрицательной суммой — выручка была зачтена,
        // когда долг возник.
        LedgerEntryTypeNames.PostpaidDebt => Math.Max(0, amountMinorUnits),
        LedgerEntryTypeNames.Refund when hasSession => -Math.Abs(amountMinorUnits),
        _ => 0
    };

    public static long Gameplay(IEnumerable<(string Kind, long AmountMinorUnits, bool HasSession)> entries) =>
        entries.Sum(entry => GameplayImpact(entry.Kind, entry.AmountMinorUnits, entry.HasSession));

    /// <summary>Движения денег по одному чеку: оплата в свой день, возврат в свой.</summary>
    public static IEnumerable<(DateTimeOffset AtUtc, long AmountMinorUnits)> GoodsMovements(
        DateTimeOffset? paidAtUtc,
        DateTimeOffset? refundedAtUtc,
        long totalMinorUnits)
    {
        if (paidAtUtc is not { } paid)
        {
            yield break;
        }

        yield return (paid, totalMinorUnits);
        if (refundedAtUtc is { } refunded)
        {
            yield return (refunded, -totalMinorUnits);
        }
    }
}
