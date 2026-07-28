using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Billing;

public interface IInvoiceGenerationRunner
{
    /// <summary>
    /// Issues invoices for every active subscription whose NextInvoiceUtc is due, advances those
    /// subscriptions, and flips overdue invoices. Returns the number of invoices issued.
    /// </summary>
    Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>
    /// Issues a subscription invoice for the subscription's current period (if not already issued)
    /// and advances the period. Returns the issued invoice, or null if one already existed.
    /// Caller is responsible for SaveChanges.
    /// </summary>
    Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        OrganizationSubscriptionEntity subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}
