namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Pricing inputs for a tariff version, decoupled from EF entities so the
/// billable-amount calculation can be unit-tested in isolation and reused by
/// the live accrued-cost display and the checkout charge.
/// </summary>
public readonly record struct TariffPricing(
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    string CurrencyCode);

/// <summary>Result of a billable-amount calculation.</summary>
public sealed record TariffComputation(int BillableMinutes, long AmountMinorUnits, string CurrencyCode);

/// <summary>
/// Pure billable-amount calculation shared by the tariff service, the live
/// accrued-cost display, and the checkout charge so the three never disagree.
/// </summary>
public static class TariffBilling
{
    /// <summary>
    /// Compute the billable amount for a requested/elapsed duration in whole minutes.
    /// Applies the minimum-billable floor then the rounding increment.
    /// Returns <c>null</c> for a non-positive duration or on arithmetic overflow.
    /// </summary>
    public static TariffComputation? ComputeForMinutes(int durationMinutes, TariffPricing pricing)
    {
        if (durationMinutes <= 0)
        {
            return null;
        }

        var billableMinutes = Math.Max(durationMinutes, pricing.MinimumBillableMinutes);
        if (pricing.RoundingIncrementMinutes > 1)
        {
            var rounded = RoundUp(billableMinutes, pricing.RoundingIncrementMinutes);
            if (rounded is null)
            {
                return null;
            }

            billableMinutes = rounded.Value;
        }

        try
        {
            var amountMinorUnits = checked(billableMinutes * pricing.PricePerMinuteMinorUnits);
            return new TariffComputation(billableMinutes, amountMinorUnits, pricing.CurrencyCode);
        }
        catch (OverflowException)
        {
            return null;
        }
    }

    /// <summary>
    /// Compute the billable amount from an elapsed wall-clock duration. Elapsed
    /// time is rounded up to whole minutes before the minimum/rounding rules
    /// apply. A non-positive elapsed time produces a zero charge (an open tab
    /// force-ended with no billable time still settles through checkout).
    /// Returns <c>null</c> only on arithmetic overflow.
    /// </summary>
    public static TariffComputation? ComputeForElapsed(TimeSpan elapsed, TariffPricing pricing)
    {
        if (elapsed <= TimeSpan.Zero)
        {
            return new TariffComputation(0, 0, pricing.CurrencyCode);
        }

        var totalMinutes = elapsed.TotalMinutes;
        if (totalMinutes >= int.MaxValue)
        {
            return null;
        }

        var elapsedMinutes = (int)Math.Ceiling(totalMinutes);
        return ComputeForMinutes(elapsedMinutes, pricing);
    }

    private static int? RoundUp(int value, int increment)
    {
        var rounded = ((long)value + increment - 1) / increment * increment;

        return rounded > int.MaxValue
            ? null
            : (int)rounded;
    }
}
