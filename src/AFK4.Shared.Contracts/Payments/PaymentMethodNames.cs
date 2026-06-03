namespace AFK4.Shared.Contracts.Payments;

public static class PaymentMethodNames
{
    public const string Cash = "cash";

    public const string CardManual = "card_manual";

    public const string Wallet = "wallet";

    public static bool IsValid(string? value) =>
        value is Cash or CardManual or Wallet;
}
