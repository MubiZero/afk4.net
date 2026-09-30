namespace AFK4.Shared.Contracts.Billing;

public static class LedgerAccountTypeNames
{
    public const string Wallet = "wallet";
    public const string Debt = "debt";
    public const string PackageTime = "package_time";
    public const string BonusTime = "bonus_time";

    /// <summary>
    /// Наличные гостя без аккаунта: запись за игру, оплаченную деньгами у стойки. Ни кошелька, ни
    /// долга у гостя нет, поэтому на балансы игроков эти записи не влияют; нужны они отчётам —
    /// выручка за игру считается по журналу, а не по кассе.
    /// </summary>
    public const string Cash = "cash";
}
