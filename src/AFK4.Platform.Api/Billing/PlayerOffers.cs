using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Operator;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Готовые предложения для экрана выбора времени (спека оболочки, §5.5). Чистые функции над
/// тарифами и балансом: суммы считает <see cref="TariffBilling"/>, тот же расчёт, что и списание.
/// </summary>
public static class PlayerOffers
{
    /// <summary>
    /// Варианты старта: час, два, три, пять. «До конца окна тарифа» нет сознательно — сессия живёт
    /// на тарифе старта до конца (решение владельца 23.09), и конец окна её не обрывает.
    /// </summary>
    public static readonly IReadOnlyList<int> StartDurations = [60, 120, 180, 300];

    /// <summary>
    /// Продление короче старта: к идущей сессии добавляют «ещё полчаса», а не «ещё пять часов».
    /// </summary>
    public static readonly IReadOnlyList<int> ExtendDurations = [30, 60, 120, 180];

    public static PlayerTariffOfferDto ForTariff(
        TariffOptionDto tariff,
        long balanceMinorUnits,
        DateTimeOffset nowUtc,
        TimeZoneInfo zone)
    {
        var pricing = new TariffPricing(
            tariff.PricePerMinuteMinorUnits,
            tariff.MinimumBillableMinutes,
            tariff.RoundingIncrementMinutes,
            tariff.CurrencyCode);
        var pricePerHour = new MoneyDto(tariff.CurrencyCode, tariff.PricePerMinuteMinorUnits * 60);

        if (!tariff.AppliesNow)
        {
            // Тариф не прячется, а говорит, когда откроется: пропавший «Утренний» читается как сбой.
            return new PlayerTariffOfferDto(
                tariff.TariffVersionId,
                tariff.TariffRuleVersionId,
                tariff.Name,
                pricePerHour,
                AppliesNow: false,
                TariffSchedule.NextStartUtc(
                    tariff.AppliesOnDaysMask,
                    tariff.AppliesFromMinuteOfDay,
                    tariff.AppliesToMinuteOfDay,
                    nowUtc,
                    zone),
                []);
        }

        return new PlayerTariffOfferDto(
            tariff.TariffVersionId,
            tariff.TariffRuleVersionId,
            tariff.Name,
            pricePerHour,
            AppliesNow: true,
            StartsAtUtc: null,
            Durations(StartDurations, pricing, balanceMinorUnits, nowUtc));
    }

    /// <summary>Варианты продления: конец сдвигается от нынешнего конца сессии, а не от «сейчас».</summary>
    public static IReadOnlyList<PlayerDurationOfferDto> ForExtension(
        TariffPricing pricing,
        long balanceMinorUnits,
        DateTimeOffset currentEndUtc) =>
        Durations(ExtendDurations, pricing, balanceMinorUnits, currentEndUtc);

    private static IReadOnlyList<PlayerDurationOfferDto> Durations(
        IReadOnlyList<int> minutes,
        TariffPricing pricing,
        long balanceMinorUnits,
        DateTimeOffset fromUtc)
    {
        var offers = new List<PlayerDurationOfferDto>(minutes.Count);
        foreach (var duration in minutes)
        {
            var charge = TariffBilling.ComputeForMinutes(duration, pricing);
            if (charge is null)
            {
                continue;
            }

            offers.Add(new PlayerDurationOfferDto(
                duration,
                charge.BillableMinutes,
                fromUtc.AddMinutes(duration),
                new MoneyDto(charge.CurrencyCode, charge.AmountMinorUnits),
                new MoneyDto(charge.CurrencyCode, balanceMinorUnits - charge.AmountMinorUnits),
                Affordable: balanceMinorUnits >= charge.AmountMinorUnits));
        }

        return offers;
    }
}
