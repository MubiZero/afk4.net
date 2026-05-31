using AFK4.Shared.Contracts.Platform.Billing;

namespace AFK4.Platform.Api.Platform.Billing;

public static class BillingPeriod
{
    public static DateTimeOffset Advance(DateTimeOffset from, string billingInterval) =>
        billingInterval == BillingIntervalNames.Yearly
            ? from.AddYears(1)
            : from.AddMonths(1);
}
