using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>Signed outstanding balance for one organization. Credit notes carry a negative amount,
/// so a credited club stops being demanded money it no longer owes.</summary>
public sealed record OrganizationBalance(
    long OutstandingMinorUnits,
    bool InArrears,
    InvoiceEntity? OldestOverdue);

public static class BillingBalance
{
    /// <summary>Caller passes the organization's unpaid invoices (issued and overdue); paid and void
    /// invoices must be filtered out before the call.</summary>
    public static OrganizationBalance Compute(IReadOnlyCollection<InvoiceEntity> unpaidInvoices)
    {
        var outstanding = unpaidInvoices.Sum(invoice => invoice.AmountMinorUnits);
        var oldestOverdue = unpaidInvoices
            .Where(invoice => invoice.Status == InvoiceStatusNames.Overdue)
            .OrderBy(invoice => invoice.DueAtUtc)
            .FirstOrDefault();

        return new OrganizationBalance(
            OutstandingMinorUnits: outstanding,
            InArrears: oldestOverdue is not null && outstanding > 0,
            OldestOverdue: oldestOverdue);
    }
}
