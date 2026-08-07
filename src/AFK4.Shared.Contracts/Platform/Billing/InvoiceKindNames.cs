namespace AFK4.Shared.Contracts.Platform.Billing;

public static class InvoiceKindNames
{
    public const string Subscription = "subscription";

    public const string Proration = "proration";

    // Full credit-note issuance ships in a later task; the constant exists now because
    // BillingBalance.Compute already treats "credit" invoices as negative-amount debt relief,
    // and this task's dunning test seeds one by hand to exercise that path.
    public const string Credit = "credit";
}
