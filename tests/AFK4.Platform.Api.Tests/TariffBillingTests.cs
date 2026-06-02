using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Tests;

public sealed class TariffBillingTests
{
    private static readonly TariffPricing Pricing = new(
        PricePerMinuteMinorUnits: 50,
        MinimumBillableMinutes: 15,
        RoundingIncrementMinutes: 5,
        CurrencyCode: "TJS");

    [Fact]
    public void ComputeForMinutes_AppliesMinimumBillableFloor()
    {
        var result = TariffBilling.ComputeForMinutes(5, Pricing);

        Assert.NotNull(result);
        Assert.Equal(15, result.BillableMinutes);
        Assert.Equal(750, result.AmountMinorUnits);
        Assert.Equal("TJS", result.CurrencyCode);
    }

    [Fact]
    public void ComputeForMinutes_RoundsUpToIncrement()
    {
        var result = TariffBilling.ComputeForMinutes(16, Pricing);

        Assert.NotNull(result);
        Assert.Equal(20, result.BillableMinutes);
        Assert.Equal(1000, result.AmountMinorUnits);
    }

    [Fact]
    public void ComputeForMinutes_ExactIncrementMultipleIsUnchanged()
    {
        var result = TariffBilling.ComputeForMinutes(25, Pricing);

        Assert.NotNull(result);
        Assert.Equal(25, result.BillableMinutes);
        Assert.Equal(1250, result.AmountMinorUnits);
    }

    [Fact]
    public void ComputeForMinutes_NonPositiveDurationReturnsNull()
    {
        Assert.Null(TariffBilling.ComputeForMinutes(0, Pricing));
        Assert.Null(TariffBilling.ComputeForMinutes(-5, Pricing));
    }

    [Fact]
    public void ComputeForMinutes_NoRoundingWhenIncrementIsOneOrLess()
    {
        var pricing = Pricing with { MinimumBillableMinutes = 1, RoundingIncrementMinutes = 1 };

        var result = TariffBilling.ComputeForMinutes(23, pricing);

        Assert.NotNull(result);
        Assert.Equal(23, result.BillableMinutes);
        Assert.Equal(1150, result.AmountMinorUnits);
    }

    [Fact]
    public void ComputeForMinutes_OverflowReturnsNull()
    {
        var pricing = Pricing with { PricePerMinuteMinorUnits = long.MaxValue };

        Assert.Null(TariffBilling.ComputeForMinutes(15, pricing));
    }

    [Fact]
    public void ComputeForElapsed_ZeroElapsedProducesZeroCharge()
    {
        var result = TariffBilling.ComputeForElapsed(TimeSpan.Zero, Pricing);

        Assert.NotNull(result);
        Assert.Equal(0, result.BillableMinutes);
        Assert.Equal(0, result.AmountMinorUnits);
        Assert.Equal("TJS", result.CurrencyCode);
    }

    [Fact]
    public void ComputeForElapsed_RoundsPartialMinuteUpThenAppliesFloor()
    {
        // 4m30s -> ceil 5 min -> floored to minimum 15.
        var result = TariffBilling.ComputeForElapsed(TimeSpan.FromSeconds(270), Pricing);

        Assert.NotNull(result);
        Assert.Equal(15, result.BillableMinutes);
        Assert.Equal(750, result.AmountMinorUnits);
    }

    [Fact]
    public void ComputeForElapsed_RoundsPartialMinuteUpThenToIncrement()
    {
        // 16m10s -> ceil 17 min -> rounded up to 20.
        var result = TariffBilling.ComputeForElapsed(TimeSpan.FromSeconds(970), Pricing);

        Assert.NotNull(result);
        Assert.Equal(20, result.BillableMinutes);
        Assert.Equal(1000, result.AmountMinorUnits);
    }

    [Fact]
    public void ComputeForElapsed_AgreesWithComputeForMinutesAtWholeMinutes()
    {
        var elapsed = TariffBilling.ComputeForElapsed(TimeSpan.FromMinutes(25), Pricing);
        var minutes = TariffBilling.ComputeForMinutes(25, Pricing);

        Assert.NotNull(elapsed);
        Assert.NotNull(minutes);
        Assert.Equal(minutes.BillableMinutes, elapsed.BillableMinutes);
        Assert.Equal(minutes.AmountMinorUnits, elapsed.AmountMinorUnits);
    }
}
