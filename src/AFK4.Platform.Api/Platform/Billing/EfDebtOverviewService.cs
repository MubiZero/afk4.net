using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfDebtOverviewService(PlatformDbContext dbContext) : IDebtOverviewService
{
    public async Task<IReadOnlyList<DebtRowDto>> GetAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var unpaid = await dbContext.Invoices.AsNoTracking()
            .Where(invoice => invoice.Status == InvoiceStatusNames.Issued || invoice.Status == InvoiceStatusNames.Overdue)
            .ToListAsync(cancellationToken);
        var unpaidByOrganization = unpaid
            .GroupBy(invoice => invoice.OrganizationId)
            .ToDictionary(group => group.Key, group => (IReadOnlyCollection<InvoiceEntity>)group.ToList());

        var organizations = await dbContext.Organizations.AsNoTracking()
            .Where(organization => organization.Status != OrganizationStatusNames.DeletionPending)
            .ToListAsync(cancellationToken);
        var subscriptions = await dbContext.OrganizationSubscriptions.AsNoTracking()
            .ToDictionaryAsync(subscription => subscription.OrganizationId, cancellationToken);

        var rows = new List<DebtRowDto>();
        foreach (var organization in organizations)
        {
            var invoices = unpaidByOrganization.TryGetValue(organization.OrganizationId, out var found)
                ? found
                : [];
            var balance = BillingBalance.Compute(invoices);
            var suspended = organization.Status == OrganizationStatusNames.Suspended;

            // A club that paid up but is still switched off is exactly as much a pending decision as
            // one that owes money — nothing un-suspends it automatically.
            var settledButSuspended = suspended && !balance.InArrears;
            if (!balance.InArrears && !settledButSuspended)
            {
                continue;
            }

            subscriptions.TryGetValue(organization.OrganizationId, out var subscription);
            var oldest = balance.OldestOverdue;
            rows.Add(new DebtRowDto(
                OrganizationId: organization.OrganizationId,
                OrganizationName: organization.Name,
                OrganizationSlug: organization.Slug,
                OrganizationStatus: organization.Status,
                SubscriptionStatus: organization.SubscriptionStatus,
                OutstandingMinorUnits: balance.InArrears ? balance.OutstandingMinorUnits : 0,
                CurrencyCode: oldest?.CurrencyCode ?? subscription?.CurrencyCode ?? "TJS",
                OldestOverdueInvoiceNumber: oldest?.Number,
                OldestOverdueInvoiceId: oldest?.InvoiceId,
                DaysOverdue: oldest is null ? 0 : Math.Max(0, (int)Math.Floor((now - oldest.DueAtUtc).TotalDays)),
                DunningStage: oldest?.DunningStage ?? 0,
                GraceUntilUtc: subscription?.PaymentGraceUntilUtc > now ? subscription.PaymentGraceUntilUtc : null,
                SettledButSuspended: settledButSuspended));
        }

        return rows
            .OrderByDescending(row => row.DaysOverdue)
            .ThenBy(row => row.OrganizationName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
