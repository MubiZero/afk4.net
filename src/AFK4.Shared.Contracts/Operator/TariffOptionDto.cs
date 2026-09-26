namespace AFK4.Shared.Contracts.Operator;

/// <summary>
/// Тариф, который можно выбрать: по чём и с какими правилами считается время. <c>AppliesNow</c>
/// считает сервер по часовому поясу филиала: клиент, повторивший этот расчёт у себя, ошибётся на
/// телефоне с чужим часовым поясом и предложит утреннюю цену вечером.
///
/// Цену по этим полям клиент НЕ считает — за этим есть расчёт на сервере: минимальное
/// оплачиваемое время и шаг округления живут в биллинге, и вторая арифметика здесь разошлась бы
/// с настоящим списанием.
///
/// <paramref name="AppliesFromMinuteOfDay"/> и <paramref name="AppliesToMinuteOfDay"/> — окно
/// местного времени клуба, минуты от полуночи. Оба пусты — круглосуточно, начало больше конца —
/// переход через полночь.
/// </summary>
public sealed record TariffOptionDto(
    Guid TariffId,
    Guid TariffVersionId,
    string Name,
    string TariffRuleVersionId,
    int VersionNumber,
    string CurrencyCode,
    long PricePerMinuteMinorUnits,
    int MinimumBillableMinutes,
    int RoundingIncrementMinutes,
    DateTimeOffset EffectiveFromUtc,
    // Биты дней недели с понедельника (1) по воскресенье (64); 0 — каждый день.
    int AppliesOnDaysMask = 0,
    int? AppliesFromMinuteOfDay = null,
    int? AppliesToMinuteOfDay = null,
    // Действует ли тариф прямо сейчас — по часам клуба, а не телефона. Важно там, где играть
    // начинают сию секунду; для брони на завтра ответ никакого значения не имеет.
    bool AppliesNow = true,
    // Тариф крутится в витрине свободного ПК.
    bool FeaturedOnPcs = false);
