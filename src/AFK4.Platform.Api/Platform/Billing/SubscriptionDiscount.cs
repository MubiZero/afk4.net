namespace AFK4.Platform.Api.Platform.Billing;

/// <summary>A negotiated discount lives beside the plan price instead of overwriting the subscription
/// amount, so changing the plan no longer silently erases what was agreed with the club.</summary>
public static class SubscriptionDiscount
{
    public static long Apply(long grossMinorUnits, int? percent, long? fixedAmountMinorUnits)
    {
        var discount = percent is not null
            ? grossMinorUnits * percent.Value / 100
            : fixedAmountMinorUnits ?? 0;

        return Math.Clamp(discount, 0, Math.Max(0, grossMinorUnits));
    }
}
