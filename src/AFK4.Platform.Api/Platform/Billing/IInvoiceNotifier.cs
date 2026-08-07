using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>
/// Enqueues transactional billing notifications to the organization owner when an invoice
/// changes state. A no-op when the org has no owner with an email on file.
/// </summary>
public interface IInvoiceNotifier
{
    Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken);

    Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken);

    Task NotifyDueSoonAsync(InvoiceEntity invoice, DateTimeOffset now, CancellationToken cancellationToken);

    Task NotifyOverdueAsync(InvoiceEntity invoice, int stage, DateTimeOffset now, CancellationToken cancellationToken);
}
