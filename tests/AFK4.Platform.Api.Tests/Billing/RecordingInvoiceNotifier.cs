using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>Records which invoice-notification hooks fired, for testing the trigger call sites in isolation.</summary>
internal sealed class RecordingInvoiceNotifier : IInvoiceNotifier
{
    public List<InvoiceEntity> Issued { get; } = [];
    public List<InvoiceEntity> Paid { get; } = [];
    public List<InvoiceEntity> Overdue { get; } = [];

    public Task NotifyIssuedAsync(InvoiceEntity invoice, CancellationToken cancellationToken)
    {
        Issued.Add(invoice);
        return Task.CompletedTask;
    }

    public Task NotifyPaidAsync(InvoiceEntity invoice, CancellationToken cancellationToken)
    {
        Paid.Add(invoice);
        return Task.CompletedTask;
    }

    public Task NotifyOverdueAsync(InvoiceEntity invoice, CancellationToken cancellationToken)
    {
        Overdue.Add(invoice);
        return Task.CompletedTask;
    }
}
