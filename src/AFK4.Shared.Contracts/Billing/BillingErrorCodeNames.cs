namespace AFK4.Shared.Contracts.Billing;

/// <summary>
/// Машинные имена отказов по деньгам игрока (возврат, ручная коррекция, гашение долга). См.
/// <see cref="Tariffs.TariffErrorCodeNames"/> — та же причина: касса и приложение игрока работают
/// на трёх языках, а английская фраза сервера в интерфейс попадать не должна.
/// </summary>
public static class BillingErrorCodeNames
{
    /// <summary>Уже вернули часть или всё — запрошенный возврат больше того, что осталось.</summary>
    public const string RefundExceedsRemaining = "refund_exceeds_remaining";

    /// <summary>Причина поправки вручную короче восьми символов — её потом никто не прочитает.</summary>
    public const string CorrectionReasonTooShort = "correction_reason_too_short";

    /// <summary>Долг уже меньше суммы платежа — кто-то погасил его первым.</summary>
    public const string DebtPaymentExceedsBalance = "debt_payment_exceeds_balance";
}
