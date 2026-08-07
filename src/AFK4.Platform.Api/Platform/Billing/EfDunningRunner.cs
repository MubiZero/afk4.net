using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfDunningRunner(
    PlatformDbContext dbContext,
    IOptions<BillingOptions> options,
    IInvoiceNotifier invoiceNotifier) : IDunningRunner
{
    private readonly BillingOptions options = options.Value;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unpaid = await dbContext.Invoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .ToListAsync(cancellationToken);
        if (unpaid.Count == 0)
        {
            return 0;
        }

        var organizationIds = unpaid.Select(invoice => invoice.OrganizationId).Distinct().ToList();
        var graceByOrganization = await dbContext.OrganizationSubscriptions
            .Where(subscription => organizationIds.Contains(subscription.OrganizationId))
            .ToDictionaryAsync(
                subscription => subscription.OrganizationId,
                subscription => subscription.PaymentGraceUntilUtc,
                cancellationToken);

        var pendingDueSoon = new List<InvoiceEntity>();
        var pendingOverdue = new List<(InvoiceEntity Invoice, int Stage)>();

        foreach (var invoice in unpaid)
        {
            if (invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc < now)
            {
                invoice.Status = InvoiceStatusNames.Overdue;
                invoice.UpdatedAtUtc = now;
            }

            // Grace is a promise not to chase, so it silences the whole ladder — including the
            // pre-due reminder — rather than only the emails after the due date.
            if (graceByOrganization.TryGetValue(invoice.OrganizationId, out var graceUntil)
                && graceUntil is not null
                && graceUntil > now)
            {
                continue;
            }

            if (invoice.DueSoonNotifiedAtUtc is null
                && now >= invoice.DueAtUtc - options.DueSoonReminderBefore
                && now < invoice.DueAtUtc)
            {
                invoice.DueSoonNotifiedAtUtc = now;
                invoice.UpdatedAtUtc = now;
                pendingDueSoon.Add(invoice);
                continue;
            }

            var stage = DueStage(invoice.DueAtUtc, now);
            if (stage > invoice.DunningStage)
            {
                invoice.DunningStage = stage;
                invoice.LastDunningAtUtc = now;
                invoice.UpdatedAtUtc = now;
                pendingOverdue.Add((invoice, stage));
            }
        }

        if (pendingDueSoon.Count == 0 && pendingOverdue.Count == 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return 0;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        foreach (var invoice in pendingDueSoon)
        {
            await invoiceNotifier.NotifyDueSoonAsync(invoice, now, cancellationToken);
        }

        foreach (var (invoice, stage) in pendingOverdue)
        {
            await invoiceNotifier.NotifyOverdueAsync(invoice, stage, now, cancellationToken);
        }

        return pendingDueSoon.Count + pendingOverdue.Count;
    }

    /// <summary>Highest ladder rung the invoice's age has reached; 0 when it is not overdue yet.
    /// An invoice first seen ten days late sends one notice, not the whole ladder in a burst.</summary>
    private int DueStage(DateTimeOffset dueAtUtc, DateTimeOffset now)
    {
        var stage = 0;
        for (var index = 0; index < options.DunningOffsetsAfterDue.Length; index++)
        {
            if (now >= dueAtUtc.AddDays(options.DunningOffsetsAfterDue[index]))
            {
                stage = index + 1;
            }
        }

        return stage;
    }
}
