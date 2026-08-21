namespace AFK4.Shared.Contracts.Billing;

public static class LedgerEntryTypeNames
{
    public const string TopUp = "top_up";
    public const string GameplayCharge = "gameplay_charge";
    public const string PackagePurchase = "package_purchase";
    public const string PackageConsumption = "package_consumption";
    public const string BonusGrant = "bonus_grant";
    public const string BonusConsumption = "bonus_consumption";
    public const string Refund = "refund";
    public const string ManualCorrection = "manual_correction";
    public const string PostpaidDebt = "postpaid_debt";
    public const string DebtPayment = "debt_payment";
    public const string WalletPayment = "wallet_payment";
    public const string Reversal = "reversal";
    public const string Cashback = "cashback";

    /// <summary>Деньги за приведённого друга — платит клуб, обоим сразу.</summary>
    public const string ReferralBonus = "referral_bonus";

    /// <summary>
    /// Заморозка под бронь: деньги остаются игроку, но потратить их второй раз уже нельзя.
    /// Оплатой не становится никогда — только снимается реверсом.
    /// </summary>
    public const string ReservationHold = "reservation_hold";

    /// <summary>
    /// Удержанная за неявку предоплата — выручка клуба, а не «деньги, которые не вернулись».
    /// Пишется только если филиал так решил, и всегда после снятия заморозки.
    /// </summary>
    public const string ReservationNoShowFee = "reservation_no_show_fee";
}
