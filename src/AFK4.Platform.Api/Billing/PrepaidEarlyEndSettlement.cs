namespace AFK4.Platform.Api.Billing;

/// <summary>
/// Сколько вернуть игроку, который встал раньше, чем кончилось оплаченное время.
///
/// Предоплаченная сессия списывается ЦЕЛИКОМ в момент старта, а не по факту: у неё нет
/// «дотарификации» на закрытии, чек-аут по ней нулевой. Поэтому ранний выход — это не
/// «доплатить разницу», а «вернуть переплату».
///
/// Вместе с деньгами обязательно разматывается кешбэк. Предоплаченный старт начисляет его на
/// ВСЮ сумму (SessionBillingService), и без этого шага получалась дыра: оплатить восемь часов,
/// встать через пять минут, забрать деньги обратно и оставить себе кешбэк за восемь часов.
/// </summary>
public readonly record struct PrepaidEarlyEndSettlement(
    long RefundMinorUnits,
    long CashbackReversalMinorUnits)
{
    public bool MovesMoney => RefundMinorUnits > 0 || CashbackReversalMinorUnits > 0;

    /// <param name="chargedMinorUnits">
    /// Сколько списано за время этой сессии при старте и продлениях; положительное.
    /// </param>
    /// <param name="actualPriceMinorUnits">
    /// Цена фактически проведённого времени ПО ПРАВИЛАМ ТАРИФА (минимальная тарифицируемая
    /// длительность и шаг округления уже применены — см. <see cref="TariffBilling"/>). Именно
    /// поэтому «по факту» не означает «по секундам»: при минимуме в час пятиминутная сессия
    /// стоит час, ровно как у стойки.
    /// </param>
    /// <param name="cashbackMinorUnits">Кешбэк, начисленный за эту сессию; ноль, если его не было.</param>
    public static PrepaidEarlyEndSettlement Compute(
        long chargedMinorUnits,
        long actualPriceMinorUnits,
        long cashbackMinorUnits)
    {
        if (chargedMinorUnits <= 0)
        {
            return new PrepaidEarlyEndSettlement(0, 0);
        }

        // Доплату здесь не делаем: если фактическое время дороже оплаченного (сессию продлили
        // мимо этого пути или часы разъехались), это случай для стойки, а не для тихого списания
        // с кошелька без ведома человека.
        var refund = Math.Max(0, chargedMinorUnits - Math.Max(0, actualPriceMinorUnits));
        if (refund == 0)
        {
            return new PrepaidEarlyEndSettlement(0, 0);
        }

        // Кешбэк снимается ровно в той доле, в какой вернулись деньги. Округление ВНИЗ намеренно:
        // при дробной доле недобрать четверть дирама у игрока лучше, чем забрать лишнюю.
        var cashbackReversal = cashbackMinorUnits <= 0
            ? 0
            : (long)((System.Numerics.BigInteger)cashbackMinorUnits * refund / chargedMinorUnits);

        return new PrepaidEarlyEndSettlement(refund, cashbackReversal);
    }
}
