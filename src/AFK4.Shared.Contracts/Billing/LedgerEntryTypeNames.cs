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
    public const string Reversal = "reversal";
}
