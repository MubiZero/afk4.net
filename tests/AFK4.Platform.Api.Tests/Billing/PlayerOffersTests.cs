using AFK4.Platform.Api.Billing;
using AFK4.Shared.Contracts.Operator;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>
/// Предложения экрана выбора считает тот же расчёт, что и списание: экран не имеет права обещать
/// одну сумму, чтобы касса списала другую.
/// </summary>
public sealed class PlayerOffersTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public void ATariffThatAppliesNow_OffersHoursPricedLikeTheCharge()
    {
        // 5 сомони в час: 500 дирамов / 60 минут не делится, поэтому цена в минуту — 9 дирамов.
        var tariff = Tariff(pricePerMinute: 9, minimumMinutes: 1, roundingMinutes: 1, appliesNow: true);

        var offer = PlayerOffers.ForTariff(tariff, balanceMinorUnits: 1_500, Now, TimeZoneInfo.Utc);

        Assert.True(offer.AppliesNow);
        Assert.Equal([60, 120, 180, 300], offer.Options.Select(option => option.Minutes));
        var hour = offer.Options[0];
        Assert.Equal(TariffBilling.ComputeForMinutes(60, new TariffPricing(9, 1, 1, "TJS"))!.AmountMinorUnits, hour.Amount.MinorUnits);
        Assert.Equal(Now.AddMinutes(60), hour.EndsAtUtc);
        Assert.Equal(1_500 - 540, hour.BalanceAfter.MinorUnits);
        Assert.True(hour.Affordable);
        // Пять часов — 2 700, денег полторы тысячи: вариант показан, но честно помечен.
        Assert.False(offer.Options[3].Affordable);
    }

    [Fact]
    public void RoundingOfTheTariff_ShowsInTheBillableMinutes()
    {
        var tariff = Tariff(pricePerMinute: 10, minimumMinutes: 90, roundingMinutes: 30, appliesNow: true);

        var offer = PlayerOffers.ForTariff(tariff, balanceMinorUnits: 100_000, Now, TimeZoneInfo.Utc);

        // Час при минимуме полтора часа оплачивается как девяносто минут.
        Assert.Equal(90, offer.Options[0].BillableMinutes);
        Assert.Equal(900, offer.Options[0].Amount.MinorUnits);
    }

    [Fact]
    public void ATariffThatIsClosedNow_SaysWhenItOpens_AndOffersNothingYet()
    {
        var morning = Tariff(pricePerMinute: 5, minimumMinutes: 1, roundingMinutes: 1, appliesNow: false) with
        {
            AppliesFromMinuteOfDay = 8 * 60,
            AppliesToMinuteOfDay = 12 * 60
        };

        var offer = PlayerOffers.ForTariff(morning, balanceMinorUnits: 10_000, Now, TimeZoneInfo.Utc);

        Assert.False(offer.AppliesNow);
        Assert.Empty(offer.Options);
        Assert.Equal(new DateTimeOffset(2026, 9, 25, 8, 0, 0, TimeSpan.Zero), offer.StartsAtUtc);
    }

    [Fact]
    public void AnExtension_MovesTheEndFromTheCurrentEnd()
    {
        var currentEnd = Now.AddMinutes(40);

        var options = PlayerOffers.ForExtension(new TariffPricing(10, 1, 1, "TJS"), 10_000, currentEnd);

        Assert.Equal([30, 60, 120, 180], options.Select(option => option.Minutes));
        Assert.Equal(currentEnd.AddMinutes(30), options[0].EndsAtUtc);
    }

    private static TariffOptionDto Tariff(long pricePerMinute, int minimumMinutes, int roundingMinutes, bool appliesNow) =>
        new(
            TariffId: Guid.NewGuid(),
            TariffVersionId: Guid.NewGuid(),
            Name: "Общий",
            TariffRuleVersionId: Guid.NewGuid().ToString("D"),
            VersionNumber: 1,
            CurrencyCode: "TJS",
            PricePerMinuteMinorUnits: pricePerMinute,
            MinimumBillableMinutes: minimumMinutes,
            RoundingIncrementMinutes: roundingMinutes,
            EffectiveFromUtc: Now.AddDays(-1),
            AppliesOnDaysMask: 0,
            AppliesFromMinuteOfDay: null,
            AppliesToMinuteOfDay: null,
            AppliesNow: appliesNow);
}
