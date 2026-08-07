using AFK4.Platform.Api.Platform.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

public sealed class SubscriptionDiscountTests
{
    [Fact]
    public void Apply_NoDiscount_IsZero() =>
        Assert.Equal(0, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: null));

    [Fact]
    public void Apply_Percent_RoundsDownToMinorUnit() =>
        Assert.Equal(87000, SubscriptionDiscount.Apply(290000, percent: 30, fixedAmountMinorUnits: null));

    [Fact]
    public void Apply_FixedAmount_IsTakenAsIs() =>
        Assert.Equal(50000, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: 50000));

    [Fact]
    public void Apply_FixedAmountLargerThanGross_FloorsAtGross() =>
        Assert.Equal(290000, SubscriptionDiscount.Apply(290000, percent: null, fixedAmountMinorUnits: 400000));
}
