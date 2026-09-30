namespace AFK4.Shared.Contracts.Billing;

public static class BillingModeNames
{
    public const string PrepaidWallet = "prepaid_wallet";
    public const string PostpaidDebt = "postpaid_debt";
    public const string Package = "package";

    /// <summary>
    /// Гость без аккаунта платит наличными у стойки: за фиксированное время — вперёд при старте и
    /// при каждом продлении. Предоплата, как кошелёк, только деньги идут в кассу смены, а не со
    /// счёта: расчёт на закрытии ничего не добавляет, а ранний уход ничего не возвращает сам.
    /// Открытый счёт гостя отдельного режима не имеет — это пустой режим с тарифом, расчёт по
    /// факту на «Завершить и рассчитать».
    /// </summary>
    public const string PrepaidCash = "prepaid_cash";
}
