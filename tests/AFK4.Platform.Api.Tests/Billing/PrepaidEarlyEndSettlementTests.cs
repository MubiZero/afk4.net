using AFK4.Platform.Api.Billing;

namespace AFK4.Platform.Api.Tests.Billing;

/// <summary>
/// Расчёт возврата за недоигранное время. Каждая граница здесь — деньги, и ошибка в любой из
/// них не видна ни на одном экране: она проявится расхождением в леджере через недели.
/// </summary>
public sealed class PrepaidEarlyEndSettlementTests
{
    [Fact]
    public void RefundsTheUnusedPart()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(
            chargedMinorUnits: 120_00, actualPriceMinorUnits: 30_00, cashbackMinorUnits: 0);

        Assert.Equal(90_00, settlement.RefundMinorUnits);
    }

    [Fact]
    public void FullyUsedSessionRefundsNothing()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(120_00, 120_00, 0);

        Assert.Equal(0, settlement.RefundMinorUnits);
        Assert.False(settlement.MovesMoney);
    }

    // Фактическое время дороже оплаченного — не наш случай: тихо списывать с кошелька за спиной
    // человека нельзя, это разбирают у стойки.
    [Fact]
    public void OverrunNeverTurnsIntoACharge()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(120_00, 500_00, 0);

        Assert.Equal(0, settlement.RefundMinorUnits);
        Assert.Equal(0, settlement.CashbackReversalMinorUnits);
    }

    // Ради этого расчёт и существует отдельно: оплатить восемь часов, встать через пять минут,
    // забрать деньги и оставить кешбэк за восемь часов — так было бы без разматывания.
    [Fact]
    public void CashbackIsUnwoundInProportionToTheRefund()
    {
        // Списано 100, кешбэк 10 (10%), фактически наиграно на 25 → вернуть 75, снять 7.5 → 7.
        var settlement = PrepaidEarlyEndSettlement.Compute(100_00, 25_00, 10_00);

        Assert.Equal(75_00, settlement.RefundMinorUnits);
        Assert.Equal(7_50, settlement.CashbackReversalMinorUnits);
    }

    [Fact]
    public void FullRefundUnwindsAllCashback()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(100_00, 0, 10_00);

        Assert.Equal(100_00, settlement.RefundMinorUnits);
        Assert.Equal(10_00, settlement.CashbackReversalMinorUnits);
    }

    // Округление вниз: недобрать долю единицы у игрока лучше, чем забрать лишнюю.
    [Fact]
    public void CashbackReversalRoundsInThePlayersFavour()
    {
        // Кешбэк 10, возвращается треть → 3.33 → снимаем 3.
        var settlement = PrepaidEarlyEndSettlement.Compute(chargedMinorUnits: 300, actualPriceMinorUnits: 200, cashbackMinorUnits: 10);

        Assert.Equal(100, settlement.RefundMinorUnits);
        Assert.Equal(3, settlement.CashbackReversalMinorUnits);
    }

    [Fact]
    public void NoCashbackMeansNothingToUnwind()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(100_00, 25_00, 0);

        Assert.Equal(0, settlement.CashbackReversalMinorUnits);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void NothingChargedMeansNothingToSettle(long charged)
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(charged, 0, 10_00);

        Assert.False(settlement.MovesMoney);
    }

    // Крупные суммы не должны переполнять промежуточное произведение кешбэка на возврат.
    [Fact]
    public void LargeAmountsDoNotOverflow()
    {
        var settlement = PrepaidEarlyEndSettlement.Compute(
            chargedMinorUnits: long.MaxValue / 2,
            actualPriceMinorUnits: 0,
            cashbackMinorUnits: long.MaxValue / 4);

        Assert.Equal(long.MaxValue / 2, settlement.RefundMinorUnits);
        Assert.Equal(long.MaxValue / 4, settlement.CashbackReversalMinorUnits);
    }
}
