using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Platform.Billing;

public sealed class EfInvoiceGenerationRunner(
    PlatformDbContext dbContext,
    IOptions<BillingOptions> options,
    IInvoiceNotifier invoiceNotifier) : IInvoiceGenerationRunner
{
    private readonly BillingOptions options = options.Value;

    public async Task<int> RunAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var dueSubscriptions = await dbContext.OrganizationSubscriptions
            .Where(subscription =>
                subscription.Status == SubscriptionStatusNames.Active &&
                subscription.NextInvoiceUtc != null &&
                subscription.NextInvoiceUtc <= now)
            .ToListAsync(cancellationToken);

        var issued = 0;
        foreach (var subscription in dueSubscriptions)
        {
            var invoice = await GenerateForSubscriptionAsync(subscription, now, cancellationToken);
            if (invoice is not null)
            {
                await InvoiceNumbering.SaveAsync(dbContext, invoice, cancellationToken);
                await invoiceNotifier.NotifyIssuedAsync(invoice, cancellationToken);
                issued++;
            }
        }

        return issued;
    }

    public async Task<InvoiceEntity?> GenerateForSubscriptionAsync(
        OrganizationSubscriptionEntity subscription,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var alreadyIssued = await dbContext.Invoices.AnyAsync(invoice =>
            invoice.OrganizationId == subscription.OrganizationId &&
            invoice.Kind == InvoiceKindNames.Subscription &&
            invoice.PeriodStartUtc == subscription.CurrentPeriodStartUtc &&
            invoice.Status != InvoiceStatusNames.Void,
            cancellationToken);
        if (alreadyIssued)
        {
            return null;
        }

        // Тариф за ПК считает ПК в момент счёта: подписка хранит лишь последнюю оценку.
        var plan = await dbContext.SubscriptionPlans.AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.PlanCode == subscription.PlanCode, cancellationToken);
        var devices = 0;
        if (plan is not null && ClubPlans.IsPerDevice(plan))
        {
            devices = await ClubPlans.ApprovedDevicesAsync(dbContext, subscription.OrganizationId, cancellationToken);
            subscription.AmountMinorUnits = ClubPlans.AmountFor(plan, devices);
            if (subscription.AmountMinorUnits <= 0)
            {
                // Десять ПК и меньше — платить не за что: «платный» тариф без платы отменил бы
                // бесплатный с его рекламой и лимитами (спека тарифов клуба, §2).
                var free = await dbContext.SubscriptionPlans.AsNoTracking()
                    .SingleOrDefaultAsync(candidate => candidate.PlanCode == OrganizationPlanCodeNames.Free, cancellationToken);
                var organization = await dbContext.Organizations
                    .SingleOrDefaultAsync(candidate => candidate.OrganizationId == subscription.OrganizationId, cancellationToken);
                if (free is not null && organization is not null)
                {
                    ClubPlans.MoveToFree(organization, subscription, free, now);
                    return null;
                }
            }
        }

        var number = await InvoiceNumbering.NextNumberAsync(dbContext, cancellationToken);

        var gross = subscription.AmountMinorUnits;
        var discountApplies = subscription.DiscountUntilUtc is null || subscription.DiscountUntilUtc > now;
        var discount = discountApplies
            ? SubscriptionDiscount.Apply(gross, subscription.DiscountPercent, subscription.DiscountAmountMinorUnits)
            : 0;
        // Бесплатный месяц за приведённый клуб обнуляет счёт целиком и тратится одним счётом.
        var freeMonth = gross > 0 && subscription.FreeMonths > 0;
        if (freeMonth)
        {
            discount = gross;
            subscription.FreeMonths--;
        }

        var invoice = new InvoiceEntity
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = subscription.OrganizationId,
            Number = number,
            Kind = InvoiceKindNames.Subscription,
            PeriodStartUtc = subscription.CurrentPeriodStartUtc,
            PeriodEndUtc = subscription.CurrentPeriodEndUtc,
            IssuedAtUtc = now,
            DueAtUtc = now.Add(options.InvoiceDueAfter),
            AmountMinorUnits = gross - discount,
            GrossAmountMinorUnits = gross,
            DiscountMinorUnits = discount,
            CurrencyCode = subscription.CurrencyCode,
            Status = InvoiceStatusNames.Issued,
            Description = $"Subscription {subscription.PlanCode} " +
                $"({subscription.CurrentPeriodStartUtc:yyyy-MM-dd} – {subscription.CurrentPeriodEndUtc:yyyy-MM-dd})" +
                (plan is not null && ClubPlans.IsPerDevice(plan) ? $", PCs: {devices}, billable: {Math.Max(0, devices - plan.IncludedDevices)}" : string.Empty) +
                (freeMonth ? ", free month for a referred club" : string.Empty),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };
        dbContext.Invoices.Add(invoice);

        subscription.CurrentPeriodStartUtc = subscription.CurrentPeriodEndUtc;
        subscription.CurrentPeriodEndUtc = BillingPeriod.Advance(subscription.CurrentPeriodEndUtc, subscription.BillingInterval);
        subscription.NextInvoiceUtc = subscription.CurrentPeriodEndUtc;
        subscription.UpdatedAtUtc = now;
        return invoice;
    }
}
