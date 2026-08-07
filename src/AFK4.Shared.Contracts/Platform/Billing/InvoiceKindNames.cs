namespace AFK4.Shared.Contracts.Platform.Billing;

public static class InvoiceKindNames
{
    public const string Subscription = "subscription";

    public const string Proration = "proration";

    /// <summary>Manually issued charge outside the subscription: setup, hardware, extra service.</summary>
    public const string OneOff = "one_off";

    /// <summary>Money owed back to the club. Carries a negative amount so the balance is arithmetic.</summary>
    public const string Credit = "credit";
}
