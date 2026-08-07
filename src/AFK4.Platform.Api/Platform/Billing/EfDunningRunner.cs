using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
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

        var notified = 0;

        // Processed one invoice at a time — mark, notify, save — rather than as a batch. The
        // notifier's outbox write (AddIfAbsentAsync) does its own SaveChangesAsync on this same
        // PlatformDbContext, so a batched "mutate everyone, then notify everyone" pass would let
        // the first notify call flush every other invoice's still-unsent-notice state early: a
        // crash right after would strand those invoices exactly like the single-invoice race this
        // was meant to close. Saving right after each invoice's own notifier call keeps that flush
        // scoped to the invoice it actually belongs to.
        foreach (var invoice in unpaid)
        {
            // <= matches the stage-1 rung boundary (offset 0 fires at now >= DueAtUtc) so the flip
            // and the first dunning notice land on the same tick instead of racing by one run.
            if (invoice.Status == InvoiceStatusNames.Issued && invoice.DueAtUtc <= now)
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
                await dbContext.SaveChangesAsync(cancellationToken);
                continue;
            }

            if (invoice.DueSoonNotifiedAtUtc is null
                && now >= invoice.DueAtUtc - options.DueSoonReminderBefore
                && now < invoice.DueAtUtc)
            {
                invoice.DueSoonNotifiedAtUtc = now;
                invoice.UpdatedAtUtc = now;
                await invoiceNotifier.NotifyDueSoonAsync(invoice, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                notified++;
                continue;
            }

            var stage = DueStage(invoice.DueAtUtc, now);
            if (stage > invoice.DunningStage)
            {
                invoice.DunningStage = stage;
                invoice.LastDunningAtUtc = now;
                invoice.UpdatedAtUtc = now;
                await invoiceNotifier.NotifyOverdueAsync(invoice, stage, now, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                notified++;
                continue;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        await SyncSubscriptionStatusesAsync(organizationIds, unpaid, graceByOrganization, now, cancellationToken);

        return notified;
    }

    /// <summary>Keeps "subscription is past_due" equivalent to "the club owes overdue money and has no
    /// live grace". Organization status is never touched here: suspension stays a human decision.</summary>
    private async Task SyncSubscriptionStatusesAsync(
        IReadOnlyCollection<Guid> organizationIds,
        IReadOnlyCollection<InvoiceEntity> unpaid,
        IReadOnlyDictionary<Guid, DateTimeOffset?> graceByOrganization,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var subscriptions = await dbContext.OrganizationSubscriptions
            .Where(subscription => organizationIds.Contains(subscription.OrganizationId))
            .ToListAsync(cancellationToken);
        var organizations = await dbContext.Organizations
            .Where(organization => organizationIds.Contains(organization.OrganizationId))
            .ToDictionaryAsync(organization => organization.OrganizationId, cancellationToken);

        var changed = false;
        foreach (var subscription in subscriptions)
        {
            if (subscription.Status is not (SubscriptionStatusNames.Active or SubscriptionStatusNames.PastDue))
            {
                continue; // trial and cancelled subscriptions are not part of the dunning cycle
            }

            var balance = BillingBalance.Compute(
                unpaid.Where(invoice => invoice.OrganizationId == subscription.OrganizationId).ToList());
            var underGrace = graceByOrganization.TryGetValue(subscription.OrganizationId, out var graceUntil)
                && graceUntil is not null
                && graceUntil > now;

            var target = balance.InArrears && !underGrace
                ? SubscriptionStatusNames.PastDue
                : SubscriptionStatusNames.Active;

            // Grace suppresses new transitions but does not settle debt: a subscription that was
            // already past_due when grace was granted stays past_due until the money arrives.
            if (underGrace && subscription.Status == SubscriptionStatusNames.PastDue && balance.InArrears)
            {
                continue;
            }

            if (subscription.Status == target)
            {
                continue;
            }

            subscription.Status = target;
            subscription.UpdatedAtUtc = now;
            if (organizations.TryGetValue(subscription.OrganizationId, out var organization))
            {
                organization.SubscriptionStatus = target;
                organization.UpdatedAtUtc = now;
            }

            changed = true;
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
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
