using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class BillingBalanceTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-05-01T00:00:00Z");

    private static InvoiceEntity Invoice(long amount, string status, string kind = "subscription", int daysOld = 0) =>
        new()
        {
            InvoiceId = Guid.NewGuid(),
            OrganizationId = Guid.Empty,
            Kind = kind,
            Status = status,
            AmountMinorUnits = amount,
            CurrencyCode = "TJS",
            DueAtUtc = Start.AddDays(-daysOld),
            IssuedAtUtc = Start.AddDays(-daysOld)
        };

    [Fact]
    public void Compute_NoInvoices_IsZeroAndNotInArrears()
    {
        var balance = BillingBalance.Compute([]);

        Assert.Equal(0, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
        Assert.Null(balance.OldestOverdue);
    }

    [Fact]
    public void Compute_OverdueInvoice_IsInArrearsAndReportsOldest()
    {
        var older = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 10);
        var newer = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 2);

        var balance = BillingBalance.Compute([newer, older]);

        Assert.Equal(580000, balance.OutstandingMinorUnits);
        Assert.True(balance.InArrears);
        Assert.Same(older, balance.OldestOverdue);
    }

    [Fact]
    public void Compute_CreditNoteCoversOverdue_LeavesNoArrears()
    {
        var overdue = Invoice(290000, InvoiceStatusNames.Overdue, daysOld: 5);
        var credit = Invoice(-290000, InvoiceStatusNames.Issued, kind: "credit");

        var balance = BillingBalance.Compute([overdue, credit]);

        Assert.Equal(0, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
    }

    [Fact]
    public void Compute_IssuedButNotYetOverdue_IsOutstandingWithoutArrears()
    {
        var balance = BillingBalance.Compute([Invoice(290000, InvoiceStatusNames.Issued)]);

        Assert.Equal(290000, balance.OutstandingMinorUnits);
        Assert.False(balance.InArrears);
    }
}
